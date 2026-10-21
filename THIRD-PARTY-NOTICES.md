# Third-party notices

Totoro is MIT licensed (see [LICENSE](LICENSE)). It includes, or is built with, the software below, under the licenses
given. This file is shipped with the app.

## Shipped with the app

| Software | License | Copyright | Source |
|---|---|---|---|
| libvlc 3.0.24 (VideoLAN.LibVLC.Windows) | LGPL-2.1-or-later | the VideoLAN team and VLC authors | https://code.videolan.org/videolan/vlc |
| LibVLCSharp 3.10.1 (LibVLCSharp.WinUI) | LGPL-2.1-or-later | VideoLAN | https://code.videolan.org/videolan/LibVLCSharp |
| Windows Community Toolkit (CommunityToolkit.WinUI.\*, CommunityToolkit.Labs.WinUI.\*) | MIT | .NET Foundation and Contributors | https://github.com/CommunityToolkit/Windows |
| MVVM Toolkit (CommunityToolkit.Mvvm) | MIT | .NET Foundation and Contributors | https://github.com/CommunityToolkit/dotnet |
| DevWinUI | MIT | Mahdi Hosseini | https://github.com/ghost1372/DevWinUI |
| DynamicData | MIT | Roland Pheasant | https://github.com/reactivemarbles/DynamicData |
| Flurl, Flurl.Http | MIT | Todd Menier | https://github.com/tmenier/Flurl |
| GraphQL.Client | MIT | graphql-dotnet | https://github.com/graphql-dotnet/graphql-client |
| Html Agility Pack | MIT | ZZZ Projects Inc. | https://github.com/zzzprojects/html-agility-pack |
| Jikan.net (JikanDotNet) | MIT | Bartłomiej Buchała | https://github.com/Ervie/jikan.net |
| MalApi.V2 | MIT | athulrajts | https://github.com/athulrajts/MyAnimeListApi-v2-Wrapper |
| NAudio (NAudio.Core, NAudio.Wasapi) | MIT | Mark Heath | https://github.com/naudio/NAudio |
| Newtonsoft.Json | MIT | James Newton-King | https://github.com/JamesNK/Newtonsoft.Json |
| OllamaSharp | MIT | Andreas Wäscher | https://github.com/awaescher/OllamaSharp |
| SharpCompress | MIT | Adam Hathcock | https://github.com/adamhathcock/sharpcompress |
| Reactive Extensions (System.Reactive) | MIT | .NET Foundation and Contributors | https://github.com/dotnet/reactive |
| Polly, Polly.Extensions.Http | BSD-3-Clause | App vNext | https://github.com/App-vNext/Polly |
| XAML Behaviors (Microsoft.Xaml.Behaviors.WinUI.Managed) | MIT | Microsoft | https://github.com/microsoft/XamlBehaviors |
| Microsoft.Extensions.\* | MIT | .NET Foundation and Contributors | https://github.com/dotnet/extensions, https://github.com/dotnet/runtime |
| SharpDX (used by LibVLCSharp) | MIT | Alexandre Mutel | https://github.com/sharpdx/SharpDX |
| Win2D (Microsoft.Graphics.Win2D) | MIT | Microsoft Corporation | https://github.com/microsoft/Win2D |
| Windows App SDK, WebView2 | Microsoft Software License Terms | Microsoft Corporation | https://github.com/microsoft/WindowsAppSDK |

libvlc and LibVLCSharp are separate libraries the app loads; you may replace them with your own builds. Their license is
in `libvlc\win-x64\COPYING.LIB`, with where their source is in `libvlc\win-x64\NOTICE.txt`.

mpv, FFmpeg and yt-dlp aren't shipped: the MPV module's settings download them from their own releases
(shinchiro/mpv-winbuild-cmake, yt-dlp/yt-dlp) when the user asks, under their own licenses. Nor is rqbit: the rqbit
module downloads it from its own releases (ikatson/rqbit) the first time a torrent is played, or when the user asks.

## The MIT License

The software above listed as MIT is under this license, each with its own copyright line from the table:

> Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated
> documentation files (the "Software"), to deal in the Software without restriction, including without limitation the
> rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit
> persons to whom the Software is furnished to do so, subject to the following conditions:
>
> The above copyright notice and this permission notice shall be included in all copies or substantial portions of the
> Software.
>
> THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
> WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
> COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
> OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

## The BSD 3-Clause License (Polly)

> Copyright (c) 2015-2025, App vNext. All rights reserved.
>
> Redistribution and use in source and binary forms, with or without modification, are permitted provided that the
> following conditions are met:
>
> 1. Redistributions of source code must retain the above copyright notice, this list of conditions and the following
>    disclaimer.
> 2. Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following
>    disclaimer in the documentation and/or other materials provided with the distribution.
> 3. Neither the name of the copyright holder nor the names of its contributors may be used to endorse or promote
>    products derived from this software without specific prior written permission.
>
> THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES,
> INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
> DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
> SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
> SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY,
> WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
> OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

## Data

Anime data comes from AniList, MyAnimeList (with Jikan), ani.zip, Aniskip, anime-relations and AnimeThemes, under their
own terms; see the README.
