<img src="https://higgy.s3.amazonaws.com/images/v_italic.jpg" alt="can't display image" width="110"></img>

# Vitesse

Vitesse is an easy-to-use, succinct web framework for C#.

## Getting Started

While in your project directory, do
```bash
git clone https://github.com/haydenhigg/Vitesse
```
in the command line.

In your Program.cs, put the following code:
```cs
using Vitesse;
```
and it's also recommended to use a type alias for Routes if you want to use manual routing
```cs
using Routes = System.Collections.Generic.Dictionary<string, Vitesse.ServerCallback>;
```

## Creating a server

To create an HTTP server, it's as easy as:
```cs
Vitesse.Server server = new Vitesse.Server("127.0.0.1", 8080); // can be shortened to ->
var server = new Server("127.0.0.1", 8080);
```
The first argument to the Server Constructor is the host name which is used directly for initializing an HttpListener. As such, to serve on all possible hosts use an asterisk as the first argument.

### Listening on the server

The Serve method of Vitesse.Server expects a single argument of type Vitesse.ServerCallback. This is a delegate type added in Vitesse, and it takes one argument, a Vitesse.Request object, and returns a Vitesse.Response object (both implemented in Vitesse).

#### Response object
The constructor for Response takes 6 optional arguments: string body, int status, string contentType, Encoding contentEncoding, WebHeaderCollection headers, and string redirect.

Default body = "",
default status = 200,
default contentType = "text/html",
default contentEncoding = System.Text.Encoding.UTF8,
default headers is null,
default redirect is null.

If redirect is not null, then the server ignores most of the other settings and redirects to the URL.

To create a simple response that will just give a plain-text response with the string "Hello world!", you could do something like:
```cs
var res = new Response(body: "Hello world!", contentType: "text/plain");
```

#### Request object
There is little need to create one of these, but it's the argument type of Vitesse.ServerCallback, so the accessible members of it are: string Path, which if one were to access "http://127.0.0.1:8080/testDirectory/a/b/c" (our server that we initialized earlier), for example, would be "/testDirectory/a/b/c"; System.Collections.Specialized.NameValueCollection Query, which is the NameValueCollectoin of pairs of GET query parameters, and if one were to access "http://127.0.0.1:8080/anything?a=17&b=hello", for example, it would be something like {"a" => "17", "b" => "hello"}; string Url is the accessed URL; string RawUrl is used for proxies where you can get the Url that the client is trying to access; string Method is the HTTP method used (like "GET" or "POST"); string PostBody is the body from a POST request.

#### ServerCallback
A delegate type. You can either use the lambda syntax directly:
```cs
server.Serve(request => { return new Response(body: "no type signatures needed!"); });
```
or you can assign the callback to a variable:
```cs
ServerCallback callback = request => { return new Response(body: "convenient delegate if I do say so myself"); });
server.Serve(callback);
```
It would be a pain to create some very complex callbacks, however, so two are provided as a part of the Vitesse.Server class:
```cs
var routes = new Routes() {
  {"/", req => { return new Response(body: "Hello from '/'!", contentType: "text/plain"); }}
};

/*
StaticServer takes 3 optional arguments: Routes routes (to override the static file response), int parentDirectories (to specify how many directories above the current one to start searching for files), and string anchor (default is "index.html"; the file that will be served at "/"). If a route is not specified in routes, then the file that is specified will be served, and if it doesn't exist then the callback will return a 404.
*/
server.StaticServer(routes: routes, parentDirectories: 1, anchor: "someDefaultFile.html");

/*
RouteServer only serves the routes specified, and returns a 404 if the route is not handled in routes.
*/
server.RouteServer(routes);
```
Here is an example of a fully-functional HTTP server listening on 127.0.0.1 and localhost.
```cs
using System;
using Vitesse;

using Routes = System.Collections.Generic.Dictionary<string, Vitesse.ServerCallback>;

namespace serverExample
{
    class Program
    {
        static void Main(string[] args)
        {
            var server = new Server("*", 8080);

            var routes = new Routes() {
                {"/", req => { return new Response(body: "Hello from '/'!"); }}
            };
            ServerCallback callback = server.StaticServer(routes: routes, parentDirectories: 3);

            server.Serve(callback);
        }
    }
}
```
for each server instance you can get the ServingUrl (in this case "http://\*:8080/"), the Port (8080), and you can turn Logging on or off. If it's on, Logging will provide info and error messages that look something like this:
```bash
12/31/18 10:39:44 PM ---- INFO: Listening at http://*:8080/
12/31/18 10:39:50 PM ---- INFO: Trying to serve /
12/31/18 10:39:50 PM ---- 404: Requested page / not found
```

### Using the server as a proxy
The proxies in Vitesse are very underdeveloped. More work is being put into them, but for now:
```cs
server.Proxy();
```
will start a simple forward proxy.

## Using client-side tools
There are simplified web-requests as static methods of the class Vitesse.Client:
```cs
Client.Get(string url); //=> string
Client.Post(string url, Dictionary<string, string> body); //=> string

// async - to get the value from a Task<T>, get the Result property
Client.GetAsync(string url); //=> Task<string>
Client.PostAsync(string url, Dictionary<string, string> body); //=> Task<string>
```

## The ServerUtil namespace
ServerUtil is used, obviously, as utility functions for Vitesse.Server, but in case you want to access any parts of it, here's how:
```cs
// To create an error page with human readable descriptions of the error, do
ServerUtil.PageCreator.Error(int status);
// but watch out--this method doesn't provide error handling for the ArgumentError thrown if an unknown status is given.

// The ServerUtil.Convert class has static methods MimeType(string fileExtension)
ServerUtil.Convert.MimeType("jpg"); //=> "image/jpeg"
// and StatusCode(int code)
ServerUtil.Convert.StatusCode(400); //=> string[] {"bad request", "Your request was not in the proper form (or was otherwise inherently unreadable)."}
