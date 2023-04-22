using UnityEngine;
using System;
using LibVLCSharp;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using Unity.VisualScripting;

public class VideoStreamPlayer : MonoBehaviour
{
    public static LibVLC libVLC;
    public MediaPlayer mediaPlayer;

    public Renderer screen;
    public RawImage canvasScreen;

    Texture2D _vlcTexture = null;
    public RenderTexture texture = null;

    public string YoutubeURL = "https://www.youtube.com/watch?v=lOTgmf2dyPQ";

    public bool flipTextureX = false;
    public bool flipTextureY = false;

    public bool automaticallyFlipOnAndroid = true;

    public bool playOnAwake = true;

    public bool logToConsole = false;

    public bool DebugMode = false;


    #region Unity
    // Start is called before the first frame update
    private void Awake()
    {
        // Get youtube video URL from PlayerPrefs and then delete it
        if (PlayerPrefs.HasKey("YoutubeURL"))
        {
            YoutubeURL = PlayerPrefs.GetString("YoutubeURL");
            PlayerPrefs.DeleteKey("YoutubeURL");
        }

        // Setup LibVLC
        if (libVLC == null)
            CreateLibVLC();

        // Setup Screen
        if (screen == null)
            screen = GetComponent<Renderer>();
        if (canvasScreen == null)
            canvasScreen = GetComponent<RawImage>();

        // Automatically flip the texture on Android
        if (automaticallyFlipOnAndroid && Application.platform == RuntimePlatform.Android)
            flipTextureY = !flipTextureY;

        // Setup Media Player
        CreateMediaPlayer();

        // Play On Start
        if (playOnAwake)
            Open();
    }

    private void OnDestroy()
    {
        DestroyMediaPlayer();
    }

    // Update is called once per frame
    private void Update()
    {
        // Get size every frame
        uint height = 0;
        uint width = 0;
        mediaPlayer.Size(0, ref width, ref height);

        // Automatically resize output texture
        if (_vlcTexture == null || _vlcTexture.width != width || _vlcTexture.height != height)
        {
            ResizeOutputTextures(width, height);
        }

        if (_vlcTexture != null)
        {
            // Update VLC texture
            var texptr = mediaPlayer.GetTexture(width, height, out bool updated);
            if (updated)
            {
                _vlcTexture.UpdateExternalTexture(texptr);

                var flip = new Vector2(flipTextureX ? -1 : 1, flipTextureY ? -1 : 1);
                Graphics.Blit(_vlcTexture, texture, flip, Vector2.zero);
            }
        }
    }
    #endregion

    #region VLC
    public void Open(string url)
    {
        this.YoutubeURL = url;
        Open();
    }

    // Open the media
    public async Task Open()
    {
        if (mediaPlayer.Media != null)
            mediaPlayer.Media.Dispose();

        var youtubeLink = new Media(new Uri(YoutubeURL));
        await youtubeLink.ParseAsync(libVLC, MediaParseOptions.ParseNetwork);

        mediaPlayer.Media = youtubeLink.SubItems.First();
        Play();
    }

    public void Play()
    {
        mediaPlayer.Play();
    }

    public void SetVolume(int volume = 100)
    {
        mediaPlayer.SetVolume(100);
    }

    public int Volume
    {
        get
        {
            if (mediaPlayer == null)
                return 0;
            return mediaPlayer.Volume;
        }
    }

    //This returns the video orientation for the currently playing video, if there is one
    public VideoOrientation? GetVideoOrientation()
    {
        var tracks = mediaPlayer?.Tracks(TrackType.Video);

        if (tracks == null || tracks.Count == 0)
            return null;

        var orientation = tracks[0]?.Data.Video.Orientation; //At the moment we're assuming the track we're playing is the first track

        return orientation;
    }
    #endregion

    // Creates and dispose of VLC objects
    #region Internal
    // Create a new static LibVLC instance and dispose of the old one
    private void CreateLibVLC()
    {
        if (libVLC != null)
        {
            libVLC.Dispose();
            libVLC = null;
        }

        Core.Initialize(Application.dataPath);
        libVLC = new LibVLC();

        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
        libVLC.Log += (s, e) =>
        {
            try
            {
                WriteLogToFile(e.FormattedLog);
            }
            catch (Exception ex)
            {
                WriteLogToFile("Exception caught in LibVLC: \n" + ex.ToString());
            }
        };
    }

    // Create a new media player object and dispose of the old one
    private void CreateMediaPlayer()
    {
        if (mediaPlayer != null)
        {
            mediaPlayer.Dispose();
            mediaPlayer = null;
        }

        mediaPlayer = new MediaPlayer(libVLC);
    }

    // Dispose of the media player object
    private void DestroyMediaPlayer()
    {
        mediaPlayer?.Stop();
        mediaPlayer?.Dispose();
        mediaPlayer = null;
    }

    //Resize the output textures to the size of the video
    private void ResizeOutputTextures(uint px, uint py)
    {
        var texptr = mediaPlayer.GetTexture(px, py, out bool updated);
        if (px != 0 && py != 0 && updated && texptr != IntPtr.Zero)
        {
            //If the currently playing video uses the Bottom Right orientation, we have to do this to avoid stretching it.
            if (GetVideoOrientation() == VideoOrientation.BottomRight)
            {
                uint swap = px;
                px = py;
                py = swap;
            }

            _vlcTexture = Texture2D.CreateExternalTexture((int)px, (int)py, TextureFormat.RGBA32, false, true, texptr); //Make a texture of the proper size for the video to output to
            texture = new RenderTexture(_vlcTexture.width, _vlcTexture.height, 0, RenderTextureFormat.ARGB32); //Make a renderTexture the same size as vlctex

            if (screen != null)
                screen.material.mainTexture = texture;
            if (canvasScreen != null)
                canvasScreen.texture = texture;
        }
    }

    // Writes logs to a file (DEBUG ONLY)
    private void WriteLogToFile(string log)
    {
        if (DebugMode)
        {
            bool isFileOpen = false;

            while (!isFileOpen)
            {
                // Using a try catch to avoid errors if the file is already open
                try
                {
                    using (StreamWriter sw = File.AppendText("VLCLog.txt"))
                    {
                        sw.WriteLine(log);
                    }
                    isFileOpen = true;
                }
                catch (Exception ex)
                {
                    Debug.Log("Exception caught in WriteLogToFile: \n" + ex.ToString());
                }
            }
        }
    }
    #endregion
}