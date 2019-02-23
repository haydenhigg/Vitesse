// - keep working on proxies

namespace Vitesse
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Dynamic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    using ServerUtil;
    using Routes = System.Collections.Generic.Dictionary<string, Vitesse.ServerCallback>;
    using PostBody = System.Collections.Generic.Dictionary<string, string>;

    public delegate Response ServerCallback(Request request);

    public class Request
    {
        public string Path;
        public System.Collections.Specialized.NameValueCollection Query;
        public string Url;
        public string RawUrl;

        public string Method;

        public string ClientAddress;
        
        public string PostBody;

        public Request(HttpListenerRequest request) {
            this.Path = request.Url.AbsolutePath.ToString();
            this.Query = request.QueryString;
            this.Url = request.Url.OriginalString.ToString();
            this.RawUrl = request.RawUrl.ToString();
            this.Method = request.HttpMethod.ToString();
            this.ClientAddress = request.RemoteEndPoint.ToString().Split(':')[0];
            
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                this.PostBody = reader.ReadToEnd();
            }
        }
    }

    public class Response
    {
        public string Body;
        public int StatusCode;
        public string ContentType;
        public Encoding ContentEncoding;
        public WebHeaderCollection Headers;
        public string Redirect;

        private WebHeaderCollection DefaultHeaders = new WebHeaderCollection();
        private Encoding DefaultEncoding = Encoding.UTF8;

        public Response(string body = "",
                          int status = 200,
                          string contentType = "text/plain",
                          Encoding contentEncoding = null,
                          WebHeaderCollection headers = null,
                          string redirect = null) {
            this.Body = body;
            this.StatusCode = status;
            this.ContentType = contentType;
            this.ContentEncoding = contentEncoding ?? this.DefaultEncoding;
            this.Headers = headers ?? this.DefaultHeaders;
            this.Redirect = redirect;
        }
    }

    public class Client
    {
        private static HttpClient client = new HttpClient();
        
        public static async Task<string> GetAsync(string url)
        {
            return await client.GetStringAsync(url);
        }

        public static async Task<string> PostAsync(string url, PostBody unencodedBody)
        {
            var response = await client.PostAsync(url, new FormUrlEncodedContent(unencodedBody));
            return await response.Content.ReadAsStringAsync();
        }

        public static string Get(string url)
        {
            return GetAsync(url).Result;
        }

        public static string Post(string url, PostBody unencodedBody)
        {
            return PostAsync(url, unencodedBody).Result;
        }
    }

    public class Server
    {
        string ServingURL;
        string Host;
        int Port;
        HttpListener Listener = new HttpListener();

        public bool Logging = true;

        private void Log(string output, int statusCode = 0)
        {
            if (this.Logging)
                Console.WriteLine(DateTime.Now.ToString() + " ---- " + (statusCode == 0 ? "INFO" : statusCode.ToString()) + ": " + output);
        }
        private void Error(string output, int statusCode = 0)
        {
            if (this.Logging)
                Console.Error.WriteLine(DateTime.Now.ToString() + " ---- " + (statusCode == 0 ? "ERROR" : statusCode.ToString()) + ": " + output);
        }

        public Server(string host, int port)
        {
            if (!HttpListener.IsSupported)
            {
                Console.Error.WriteLine("HttpListener class is not supported.");
                return;
            }

            this.ServingURL = "http://" + host + ":" + port.ToString() + "/";
            this.Host = host;
            this.Port = port;

            this.Listener.Prefixes.Add(this.ServingURL);
        }

        public void Serve(ServerCallback callback)
        {
            this.Listener.Start();

            Log("Listening at " + this.ServingURL);

            HttpListenerContext context;
            HttpListenerRequest request;
            HttpListenerResponse response;

            byte[] buffer;

            while (this.Listener.IsListening)
            {
                try
                {
                    context = this.Listener.GetContext();
                    request = context.Request;
                    response = context.Response;

                    Response userRes;
                    
                    try
                    {
                        userRes = callback(new Request(request));
                    }
                    catch (Exception e)
                    {
                        Error(e.ToString(), 500);
                        userRes = new Response(body: File.ReadAllText("Vitesse/defaultError.html"), status: 500, contentType: "text/html");
                    }
                    
                    userRes.Headers.Add("Content-Type", userRes.ContentType);

                    response.ContentType = userRes.ContentType;
                    response.StatusCode = userRes.StatusCode;
                    response.ContentEncoding = userRes.ContentEncoding;
                    response.Headers = userRes.Headers;

                    if (userRes.Redirect != null)
                    {
                        Log("Redirecting to " + userRes.Redirect, userRes.StatusCode);

                        response.Redirect(userRes.Redirect);

                        response.OutputStream.Close();
                    }
                    else
                    {
                        buffer = userRes.ContentEncoding.GetBytes(userRes.Body);

                        response.ContentLength64 = buffer.Length;
                        
                        response.OutputStream.Write(buffer, 0, buffer.Length);
                        response.OutputStream.Close();
                    }
                }
                catch (Exception e)
                {
                    Error(e.ToString());

                    this.Listener.Stop();
                }
            }
        }

        public void Proxy()
        {
            this.Listener.Start();

            Log("Listening at " + this.ServingURL);

            HttpListenerContext context;
            HttpListenerRequest request;
            HttpListenerResponse response;

            while (this.Listener.IsListening)
            {
                try
                {
                    context = this.Listener.GetContext();
                    request = context.Request;
                    response = context.Response;

                    try
                    {
                        Log("Trying to form gateway to " + request.RawUrl);

                        WebRequest req = WebRequest.Create(request.RawUrl);
                        HttpWebResponse res = (HttpWebResponse) req.GetResponse();
                        Stream resStream = res.GetResponseStream();

                        response.ContentType = res.ContentType;
                        response.StatusCode = 200;
                        response.ContentEncoding = Encoding.UTF8;
                        response.Headers = res.Headers;

                        resStream.CopyTo(response.OutputStream);
                    }
                    catch (System.Net.WebException e)
                    {
                        Error(e.ToString(), 502);

                        byte[] buf = Encoding.UTF8.GetBytes(PageCreator.Error(502));

                        response.ContentType = "text/html";
                        response.StatusCode = 502;
                        response.ContentEncoding = Encoding.UTF8;
                        response.ContentLength64 = buf.Length;
                        
                        response.OutputStream.Write(buf, 0, buf.Length);
                    }
                    finally
                    {
                        response.OutputStream.Close();
                    }
                }
                catch (Exception e)
                {
                    Error(e.ToString());

                    this.Listener.Stop();
                }
            }
        }
        
        public ServerCallback StaticServer(Routes routes = null, int parentDirectories = 0, string anchor = "index.html")
        {
            return request => {
                routes = routes ?? new Routes();

                if (routes.ContainsKey(request.Path))
                {
                    Log("Trying to serve " + request.Path);
                    return routes[request.Path](request);
                }
                else
                {
                    if (request.Method != "GET") {
                        Error("Request method is not GET for requested page " + request.Path, 501);
                        return new Response(body: PageCreator.Error(501), status: 501, contentType: "text/html");
                    }
                    
                    try
                    {
                        StringBuilder pathPrefix = new StringBuilder();
                        for (int i = 0; i < parentDirectories; i++)
                            pathPrefix.Append("../");
                        string fileName = request.Path == "/" ? anchor : request.Path.Substring(1);

                        string text;
                        try
                        {
                            text = File.ReadAllText(pathPrefix.ToString() + fileName);
                            Log("Trying to serve " + pathPrefix.ToString() + fileName);
                            return new Response(body: text, contentType: ServerUtil.Convert.MimeType(fileName.Split('.').Last()));
                        }
                        catch (DirectoryNotFoundException)
                        {
                            throw new FileNotFoundException();
                        }
                    }
                    catch (FileNotFoundException)
                    {
                        try
                        {
                            if (request.Path == "/favicon.ico")
                            {
                                Log("Trying to serve /favicon.ico");
                                return new Response(contentType: "text/html", redirect: "https://higgy.s3.amazonaws.com/images/v_italic.jpg", status: 302);
                            }
                            else
                            {
                                Error("Requested page " + request.Path + " not found", 404);
                                return new Response(body: PageCreator.Error(404), status: 404, contentType: "text/html");
                            }
                        }
                        catch (ArgumentException)
                        {
                            Error("Could not create error page", 500);
                            return new Response(body: File.ReadAllText("Vitesse/defaultError.html"), status: 500, contentType: "text/html");
                        }
                    }
                }
            };
        }

        public ServerCallback RouteServer(Routes routes)
        {
            return request => {
                if (request.Method != "GET") {
                    Error("Request method is not GET", 501);
                    return new Response(body: PageCreator.Error(501), status: 501, contentType: "text/html");
                }

                if (routes.ContainsKey(request.Path))
                {
                    Log("Trying to serve " + request.Path);
                    return routes[request.Path](request);
                }
                else
                {
                    try
                    {
                        Error("Requested page " + request.Path + " not found", 404);
                        return new Response(body: PageCreator.Error(404), status: 404, contentType: "text/html");
                    }
                    catch (ArgumentException)
                    {
                        Error("Could not create error page", 500);
                        return new Response(body: File.ReadAllText("Vitesse/defaultError.html"), status: 500, contentType: "text/html");
                    }
                }
            };
        }
    }
}
