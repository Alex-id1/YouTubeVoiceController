using YouTubeVoiceController.DirectMLPredictor;

namespace YouTubeVoiceController{
    /// <summary>
    /// Executes YOLO-based "visual click" commands:
    ///   1) Captures the screen.
    ///   2) Splits it into tiles and runs inference.
    ///   3) Finds the highest-confidence detection of the target class.
    ///   4) Scales bounding box coordinates back to screen space.
    ///   5) Clicks the centre of the detected button
    /// </summary>
    static class YoloClickExecutor{
        /// <summary>
        /// Runs inference for <paramref name="yoloClassName"/>, clicks the best detection, and notifies the user of the result
        /// </summary>
        public static async Task ExecuteAsync(string voiceCommand, string yoloClassName, InferenceScheduler scheduler, IUserNotifier notifier){
            try{
                var hit = await LocateAsync(yoloClassName, scheduler, revealControls: true);
                if (hit == null){
                    notifier.Notify($"⚠ '{yoloClassName}' button not found on screen", NotifyLevel.Warn, speechText: "button not found");
                    return;
                }

                ClickSimulator.Click(hit.Value.X, hit.Value.Y);

                string speech = YouTubeSpeechResponses.Get(voiceCommand) ?? voiceCommand;
                notifier.Notify($"✔ {voiceCommand} (conf {hit.Value.Confidence:P0})", speechText: speech);
            }
            catch (Exception ex){
                AppLogger.Error($"YoloClickExecutor.ExecuteAsync failed for '{yoloClassName}'", ex);
                notifier.Notify($"✖ YOLO click error: {ex.Message}", NotifyLevel.Warn);
            }
        }

        /// <summary>
        /// Runs YOLO inference and returns the screen-space centre of the best detection of
        /// <paramref name="yoloClassName"/>, or null if none was found above threshold.
        /// Does NOT click. Used by callers that need the button position for further geometry
        /// (e.g. computing the video-frame focus point from the "like" button location).
        /// </summary>
        /// <param name="revealControls">If true, nudges the cursor and waits 1.5 s so the
        /// player overlay animates in. Set false when the target is outside the player overlay
        /// (e.g. like/dislike below the video)</param>
        public static async Task<(int X, int Y, float Confidence)?> LocateAsync(
            string yoloClassName, InferenceScheduler scheduler, bool revealControls = false){

            AppLogger.Info($"YoloLocate: looking for class '{yoloClassName}'");

            if (revealControls){
                MouseHelper.NudgeCursor(1);
                await Task.Delay(1500);
            }

            using var screenshot = ScreenCapture.Capture();
            var screenSize = new Size2Int(screenshot.Width, screenshot.Height);
            int modelSize = scheduler.GetInputSize().Width;

            if (modelSize == 0){
                AppLogger.Warning("YoloLocate: scheduler returned modelSize=0 => model may not be loaded");
                return null;
            }

            var tiles = TileHelper.SplitTo8Tiles(screenshot, modelSize);

            // Bottom row first - YouTube controls live there, maximises early-exit hits
            int[] inferOrder = { 4, 5, 6, 7, 0, 1, 2, 3 };

            var detections = new List<(YoloPrediction pred, TileInfo tile)>();
            int tilesRun = 0;
            bool earlyExit = false;

            foreach (int idx in inferOrder){
                var results = await scheduler.PredictAsync(tiles[idx].Image);
                tilesRun++;

                YoloPrediction? bestThisTile = null;
                foreach (var pred in results){
                    if (!pred.ClassName.Equals(yoloClassName, StringComparison.OrdinalIgnoreCase)) continue;
                    if (pred.Confidence < AppSettings.TileDetectionMinConfidence) continue;
                    detections.Add((pred, tiles[idx]));
                    if (bestThisTile == null || pred.Confidence > bestThisTile.Confidence)
                        bestThisTile = pred;
                }

                if (bestThisTile != null && bestThisTile.Confidence >= AppSettings.EarlyExitConfidence){
                    AppLogger.Info($"YoloLocate: early exit at tile[{idx}] conf={bestThisTile.Confidence:F3} ({tilesRun}/8 tiles)");
                    earlyExit = true;
                    break;
                }
            }

            AppLogger.Debug($"YoloLocate: ran {tilesRun}/8 tiles, {detections.Count} candidate(s), earlyExit={earlyExit}");

            foreach (var t in tiles) t.Dispose();

            if (detections.Count == 0){
                AppLogger.Warning($"YoloLocate: no '{yoloClassName}' detections (conf≥{AppSettings.TileDetectionMinConfidence:F2})");
                return null;
            }

            var (best, bestTile) = detections.MaxBy(d => d.pred.Confidence);

            float canvasX = best.X + bestTile.Offset.X;
            float canvasY = best.Y + bestTile.Offset.Y;
            var modelSz = scheduler.GetInputSize();
            var screenRect = CoordinateScaler.ToScreenRect(canvasX, canvasY, best.Width, best.Height, screenSize, modelSz, bestTile.ImgType);

            int x = screenRect.X + screenRect.Width / 2;
            int y = screenRect.Y + screenRect.Height / 2;
            AppLogger.Info($"YoloLocate: '{yoloClassName}' @ conf={best.Confidence:F3} screen({x},{y})");
            return (x, y, best.Confidence);
        }
    }
}