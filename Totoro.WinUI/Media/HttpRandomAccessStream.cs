using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Storage.Streams;
using Windows.Web.Http;

namespace MediaPlayerElementWithHttpClient;

class HttpRandomAccessStream : IRandomAccessStreamWithContentType
{
    private HttpClient client;
    private IInputStream inputStream;
    private ulong size;
    private ulong requestedPosition;
    private string etagHeader;
    private string lastModifiedHeader;
    private Uri requestedUri;

    // No public constructor, factory methods instead to handle async tasks.
    private HttpRandomAccessStream(HttpClient client, Uri uri)
    {
        this.client = client;
        requestedUri = uri;
        requestedPosition = 0;
    }

    static public IAsyncOperation<HttpRandomAccessStream> CreateAsync(HttpClient client, Uri uri)
    {
        HttpRandomAccessStream randomStream = new HttpRandomAccessStream(client, uri);

        return AsyncInfo.Run<HttpRandomAccessStream>(async (cancellationToken) =>
        {
            await randomStream.SendRequesAsync().ConfigureAwait(false);
            return randomStream;
        });
    }

    private async Task SendRequesAsync()
    {
        Debug.Assert(inputStream == null);

        HttpRequestMessage request = null;
        request = new HttpRequestMessage(HttpMethod.Get, requestedUri);

        request.Headers.Add("Range", String.Format("bytes={0}-", requestedPosition));

        if (!String.IsNullOrEmpty(etagHeader))
        {
            request.Headers.Add("If-Match", etagHeader);
        }

        if (!String.IsNullOrEmpty(lastModifiedHeader))
        {
            request.Headers.Add("If-Unmodified-Since", lastModifiedHeader);
        }

        HttpResponseMessage response = await client.SendRequestAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead).AsTask().ConfigureAwait(false);

        if (response.Content.Headers.ContentType != null)
        {
            this.ContentType = response.Content.Headers.ContentType.MediaType;
        }

        size = response.Content.Headers.ContentLength.Value;

        if (response.StatusCode != HttpStatusCode.PartialContent && requestedPosition != 0)
        {
            throw new Exception("HTTP server did not reply with a '206 Partial Content' status.");
        }

        if (!response.Headers.ContainsKey("Accept-Ranges"))
        {
            throw new Exception(String.Format(
                "HTTP server does not support range requests: {0}",
                "http://www.w3.org/Protocols/rfc2616/rfc2616-sec14.html#sec14.5"));
        }

        if (String.IsNullOrEmpty(etagHeader) && response.Headers.ContainsKey("ETag"))
        {
            etagHeader = response.Headers["ETag"];
        }

        if (String.IsNullOrEmpty(lastModifiedHeader) && response.Content.Headers.ContainsKey("Last-Modified"))
        {
            lastModifiedHeader = response.Content.Headers["Last-Modified"];
        }
        if (response.Content.Headers.ContainsKey("Content-Type"))
        {
            contentType = response.Content.Headers["Content-Type"];
        }

        inputStream = await response.Content.ReadAsInputStreamAsync().AsTask().ConfigureAwait(false);
    }

    private string contentType = string.Empty;

    public string ContentType
    {
        get { return contentType; }
        private set { contentType = value; }
    }

    public bool CanRead
    {
        get
        {
            return true;
        }
    }

    public bool CanWrite
    {
        get
        {
            return false;
        }
    }

    public IRandomAccessStream CloneStream()
    {
        // If there is only one MediaPlayerElement using the stream, it is safe to return itself.
        return this;
    }

    public IInputStream GetInputStreamAt(ulong position)
    {
        throw new NotImplementedException();
    }

    public IOutputStream GetOutputStreamAt(ulong position)
    {
        throw new NotImplementedException();
    }

    public ulong Position
    {
        get
        {
            return requestedPosition;
        }
    }

    public void Seek(ulong position)
    {
        if (requestedPosition != position)
        {
            if (inputStream != null)
            {
                inputStream.Dispose();
                inputStream = null;
            }
            Debug.WriteLine("Seek: {0:N0} -> {1:N0}", requestedPosition, position);
            requestedPosition = position;
        }
    }

    public ulong Size
    {
        get
        {
            return size;
        }
        set
        {
            throw new NotImplementedException();
        }
    }

    public void Dispose()
    {
        if (inputStream != null)
        {
            inputStream.Dispose();
            inputStream = null;
        }
    }

    public Windows.Foundation.IAsyncOperationWithProgress<IBuffer, uint> ReadAsync(IBuffer buffer, uint count, InputStreamOptions options)
    {
        return AsyncInfo.Run<IBuffer, uint>(async (cancellationToken, progress) =>
        {
            progress.Report(0);

            try
            {
                if (inputStream == null)
                {
                    await SendRequesAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                throw;
            }

            IBuffer result = await inputStream.ReadAsync(buffer, count, options).AsTask(cancellationToken, progress).ConfigureAwait(false);

            // Move position forward.
            requestedPosition += result.Length;
            Debug.WriteLine("requestedPosition = {0:N0}", requestedPosition);

            return result;
        });
    }

    public Windows.Foundation.IAsyncOperation<bool> FlushAsync()
    {
        throw new NotImplementedException();
    }

    public Windows.Foundation.IAsyncOperationWithProgress<uint, uint> WriteAsync(IBuffer buffer)
    {
        throw new NotImplementedException();
    }
}