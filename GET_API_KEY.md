# How to Get a YouTube Data API v3 Key

1. Go to [Google Cloud Console](https://console.cloud.google.com/)

2. **Create a new project**
   - Click the project dropdown at the top -> **New Project**
   - Enter any name (e.g. `YouTubeVoiceController`) -> **Create**

3. **Enable YouTube Data API v3**
   - In the left menu: **APIs & Services** -> **Library**
   - Search for `YouTube Data API v3`
   - Click on it -> **Enable**

4. **Create an API Key**
   - In the left menu: **APIs & Services** -> **Credentials**
   - Click **+ Create Credentials** -> **API Key**
   - Your key will appear immediately - copy it

5. **Restrict the key (recommended)**
   - Click **Edit API Key** (pencil icon)
   - Under **API restrictions** -> **Restrict key** -> select `YouTube Data API v3`
   - Click **Save**

6. **Add the key to the app**
   - Download the source code
   - Build and run the project in Visual Studio
   - Paste your key into the API Key field in the app window
   - You only need to do this once - the app will remember your key. To use a different key, simply paste the new one into the API Key field

> **Note:** The free quota is 10,000 units/day. One search request costs 100 units - that is 100 searches per day, more than enough for personal use.
