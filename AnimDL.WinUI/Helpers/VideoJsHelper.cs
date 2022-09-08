namespace AnimDL.WinUI.Helpers;

[Obsolete("This was used for a webview based video player, use Native MediaPlayerElement instead")]
public class VideoJsHelper
{
    const string PlayerFormat = @"<!DOCTYPE html>
<html>
    <head>
        <meta charset=utf-8 />
        <title>Your title</title>
        <link href=""https://unpkg.com/video.js/dist/video-js.css"" rel=""stylesheet"">
        <script src=""https://unpkg.com/video.js/dist/video.js""></script>
        <script src=""https://unpkg.com/@videojs/http-streaming/dist/videojs-http-streaming.js""></script>
    </head>

    <body>
        <video id = ""my_video_1"" class=""video-js vjs-default-skin vjs-fill vjs-big-play-centered"" controls autoplay preload=""auto"">
            <source src = ""{0}"" type=""application/x-mpegURL"">
        </video>

        <script>
            var player = videojs('my_video_1');
            player.ready(function () {{
                this.responsive(true);
                var obj = new Object();
                obj.MessageType = ""Ready"";
                window.chrome.webview.postMessage(obj);
                this.on('timeupdate', function () {{
                   var obj = new Object();
                   obj.MessageType = ""TimeUpdate"";
                   obj.Content  = this.currentTime().toString();
                   window.chrome.webview.postMessage(obj);
                }})
                this.on('durationchange', function () {{
                   var obj = new Object();
                   obj.MessageType = ""DurationUpdate"";
                   obj.Content  = this.duration().toString();
                   window.chrome.webview.postMessage(obj);
                }})
                this.on('ended', function () {{
                   var obj = new Object();
                   obj.MessageType = ""Ended"";
                   window.chrome.webview.postMessage(obj);
                }})
                this.on('canplay', function () {{
                   var obj = new Object();
                   obj.MessageType = ""CanPlay"";
                   window.chrome.webview.postMessage(obj);
                }})
                this.on('play', function () {{
                    var obj = new Object();
                    obj.MessageType = ""Play""
                    window.chrome.webview.postMessage(obj);
                    this.requestFullscreen();
                }})
                this.on('pause', function () {{
                    var obj = new Object();
                    obj.MessageType = ""Pause""
                    window.chrome.webview.postMessage(obj);
                }})
                this.on('seeked', function () {{
                    var obj = new Object();
                    obj.MessageType = ""Seeked""
                    window.chrome.webview.postMessage(obj);
                }})
              }});
              window.chrome.webview.addEventListener('message', event => {{
                if(event.data.MessageType == ""Play"")
                {{
                    player.currentTime(event.data.StartTime);
                    player.requestFullscreen();
                    player.play();
                }}
              }});
        </script>
    </body>
</html>";

    public static string GetPlayerHtml(string url)
    {
        var result = string.Format(PlayerFormat, url);
        return string.Format(PlayerFormat, url);
    }
}
