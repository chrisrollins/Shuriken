// Shuriken - Webserver Micro-Framework for C#
// Created by Christopher Rollins


// /////////////////////////////////////////////

// Copyright (c) 2016 Christopher Rollins

// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

////////////////////////////////////////////

/*
The Server class provides the core functionality. Call Shuriken.Server.Start method to start your webserver.
*/

namespace Shuriken
{
	using System;
	using System.Net;
	using System.IO;
	using System.Collections.Generic;
	using System.Collections.Concurrent;
	using System.Threading;
	using System.Threading.Tasks;
	using System.Reflection;
	using System.Text;
	using System.Collections.Specialized;

    public static partial class Server
	{
		private static bool started = false;
		private static string path = Environment.CurrentDirectory + "/";
		private static string SettingsConfigPath = path + "shuriken.settings.configuration";
		private static string ExceptionsConfigPath = path + "shuriken.exceptions.configuration";
		private static Dictionary<string, string> MIMETypeList = new Dictionary<string, string>();
		private static Dictionary<string, string> SettingsConfigDict = new Dictionary<string, string>();
		private static Dictionary<string, string> ExceptionsConfigDict = new Dictionary<string, string>();
		private static Dictionary<string, int> IPAddressConnections = new Dictionary<string, int>();

		//Config Defaults
		private static int URICharLimit = 255;
		private static bool ShowServerMsgs = true;
		private static bool ShowExceptions = true;
		private static int ProcessedTemplateMaxSize = 5242880;
		private static bool Templating = false;
		private static int FileCache_FileLimit = 1000;
		private static int ListenPort = 5000;
		private static int MaxConnectionsPerIP = -1;
		private static string StaticDirName = "static";
		private static string HTMLDirName = "html";
		private static string HTTPErrorDirName = "httperrors";

        //Websockets stuff
        private static bool WebSocketsEnabled = false;
        #pragma warning disable 0414
        private static string WebSocketSubProtocol = null;
        #pragma warning restore 0414

        //maps the directories for file extensions
        private static Dictionary<string, string> FileExtensionDirectories = null; //it will get created if needed.
        
        public static string StaticDirectory { get { return StaticDirName; } }
        public static string HTMLDirectory {get {return HTMLDirName;}}
		public static string HTTPErrorDirectory {get {return HTTPErrorDirName;}}

		//used when http error files are missing
		private static byte[] Hardcoded404Response = Encoding.UTF8.GetBytes("<!DOCTYPE html><html><head><title>404 - File Not Found</title></head><style type='text/css'>body{background-color: #000;}h2{text-align: center;font-family: sans-serif;color: #fff;}</style><body><br><br><h2>404 error</h2><br><h2>file not found</h2></body></html>");
		private static byte[] Hardcoded400Response = Encoding.UTF8.GetBytes("<!DOCTYPE html><html><head><title>400 - Bad Request</title></head><style type='text/css'>body{background-color: #000;}h2{text-align: center;font-family: sans-serif;color: #fff;}</style><body><br><br><h2>404 error</h2><br><h2>bad request</h2></body></html>");
		private static byte[] Hardcoded414Response = Encoding.UTF8.GetBytes("<!DOCTYPE html><html><head><title>414 - URI Too Long</title></head><style type='text/css'>body{background-color: #000;}h2{text-align: center;font-family: sans-serif;color: #fff;}</style><body><br><br><h2>414 error</h2><br><h2>uri too long</h2></body></html>");
		private static byte[] Hardcoded500Response = Encoding.UTF8.GetBytes("<!DOCTYPE html><html><head><title>500 - Internal Server Error</title></head><style type='text/css'>body{background-color: #000;}h2{text-align: center;font-family: sans-serif;color: #fff;}</style><body><br><br><h2>500 error</h2><br><h2>internal server error</h2></body></html>");
        private static byte[] GenericErrorResponse = Encoding.UTF8.GetBytes("<!DOCTYPE html><html><head><title>Unknown Error</title></head><style type='text/css'>body{background-color: #000;}h2{text-align: center;font-family: sans-serif;color: #fff;}</style><body><br><br><h2>Unknown Error</h2></body></html>");
                        
        private class ReqState
        {
            public bool requestedWS;
            public bool recieved;
            public bool finished;
        }

        private class Request
        {
            public HttpListener listener;
            public ReqState state;
            public HttpListenerContext context;
           
            public Request(HttpListener listener, ref ReqState state)
            {
                this.listener = listener;
                this.state = state;
                this.context = null;
            }
        }
        
        public static void Start()
        {
            if (!started)
			{
				started = true;
				ProcessConfig();

				MIMETypeList.Add(".html", "text/html");
				MIMETypeList.Add(".htm", "text/html");
				MIMETypeList.Add(".css", "text/css");
				MIMETypeList.Add(".png", "image/png");
				MIMETypeList.Add(".jpg", "image/jpeg");
				MIMETypeList.Add(".bmp", "image/bmp");

				Routes.init_SendServerSharedItems();

				Thread ServerThread = new Thread(() =>
				{
                    HttpListener listener = new HttpListener();
                    listener.Start();
                    listener.Prefixes.Add(String.Join(null, "http://*:", ListenPort.ToString(), "/"));

                    Server.Print("Listening on port {0}", ListenPort);
                    
					while(true)
					{   
                        try
                        {
                            ReqState completion = new ReqState();
                            Request currentReq = new Request(listener, ref completion);
                            IAsyncResult result = listener.BeginGetContext(new AsyncCallback(HandleRequest), currentReq);
                            result.AsyncWaitHandle.WaitOne();
                            if (Server.WebSocketsEnabled && Server.HWS != null)
                            {
                                Task.Run(() =>
                                {
                                    CheckForWSReq(currentReq);
                                });
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                        }
					}
				});

                ServerThread.Start();

                async void CheckForWSReq(Request req)
                {
                    while (!req.state.finished && !req.state.requestedWS)
                    {}
                    if(req.state.requestedWS)
                    {
                        await Server.HWS(req.context);
                    }
                }
            }
        }

        //point the server to a directory for one or more file extensions.
        public static void SetFileExtensionDirectory(string directory, params string[] extensions)
        {
            foreach (string ext in extensions)
            {
                SetFileExtensionDirectory(directory, ext);
            }
        }

        //for one file extension.
        public static void SetFileExtensionDirectory(string directory, string extension)
        {
            if (FileExtensionDirectories == null)
            {
                FileExtensionDirectories = new Dictionary<string, string>();
                Server.GetFileExtDir = delegate(string ext)
                {
                    return (FileExtensionDirectories.TryGetValue(ext, out string result)) ? result : StaticDirName;
                };
            }
            if (extension[0] != '.')
            {
                extension = String.Join(null, ".", extension);
            }
            FileExtensionDirectories[extension] = directory;
        }

        public static void Print(object msg, params object[] arg)
		{
			if(ShowServerMsgs)
			{
				if(msg is string)
				{
					Task.Run(() => Console.WriteLine((string)msg, arg));
				}
				else
				{
					Task.Run(() => Console.WriteLine(msg));
				}
			}
		}

		public static void PrintException(Exception e)
		{
			if(ShowExceptions)
			{
				Task.Run(() => {
					Console.WriteLine(e);
					Console.WriteLine("------------------\nShuriken caught the above exception and is still running.");
				});
			}
		}

		public static void init_myTryRoute(Func<string, string> f)
		{
			if(!myTryRoute_init)
			{
				myTryRoute_init = true;
				myTryRoute = f;
			}
		}

		[ThreadStatic] public static object _TemplateData = null;

		//private methods

		private static bool myTryRoute_init = false;
		private static Func<string, string> myTryRoute;

        private static Func<string, string> GetFileExtDir = delegate (string extension)
        {
            return StaticDirName;
        };

		private static void ClearThreadStatics()
		{
            _TemplateData = null;
		}

		private static void ProcessConfig()
		{
			Func<string, int> ExtractInt = delegate(string input)
			{
				int result = 0;
				int numCount = 1;

				for(int count = (input.Length - 1); count >= 0; count--)
				{
					char ch = input[count];
					if(ch < 58 && ch > 47)
					{
						result += (ch - 48)*numCount;
						numCount *= 10;
					}
				}

				return result;
			};

			try
			{
				if(LoadConfigFileIntoDictionary(SettingsConfigPath, SettingsConfigDict))
					Server.Print("Settings configuration file loaded.");
				else
					Server.Print("Settings configuration file not found. Using defaults.");

				if(LoadConfigFileIntoDictionary(ExceptionsConfigPath, ExceptionsConfigDict))
					Server.Print("Exceptions configuration file loaded.");
				else
					Server.Print("Exceptions configuration file not found. Using defaults.");

				string outvar;
				if(SettingsConfigDict.TryGetValue("PORT", out outvar))
					ListenPort = ExtractInt(outvar);
				if(SettingsConfigDict.TryGetValue("STATIC_FILE_DIRECTORY", out outvar))
					StaticDirName = outvar;
				if(SettingsConfigDict.TryGetValue("HTML_FILE_DIRECTORY", out outvar))
					HTMLDirName = outvar;
				if(SettingsConfigDict.TryGetValue("HTTP_ERROR_DIRECTORY", out outvar))
					HTTPErrorDirName = outvar;
				if(SettingsConfigDict.TryGetValue("URI_CHARACTER_LIMIT", out outvar))
					URICharLimit = ExtractInt(outvar);
				if(SettingsConfigDict.TryGetValue("SHOW_CONSOLE_MESSAGES", out outvar))
					ShowServerMsgs = (outvar == "true");
				if(SettingsConfigDict.TryGetValue("SHOW_EXCEPTIONS", out outvar))
					ShowExceptions = (outvar == "true");
				if(SettingsConfigDict.TryGetValue("ENABLE_TEMPLATING", out outvar))
					Templating = (outvar == "true");
				if(SettingsConfigDict.TryGetValue("MAX_TEMPLATE_SIZE_IN_BYTES", out outvar))
					ProcessedTemplateMaxSize = ExtractInt(outvar);
				if(SettingsConfigDict.TryGetValue("CACHE_FILE_LIMIT", out outvar))
					FileCache_FileLimit = ExtractInt(outvar);
				if(SettingsConfigDict.TryGetValue("MAX_CONNECTIONS_PER_IP", out outvar))
					MaxConnectionsPerIP = ExtractInt(outvar);
			}
			catch(Exception e)
			{
				Console.WriteLine(e);
				Console.WriteLine("Warning. An exception occurred while trying to load configuration settings.");
			}

            string path = Server.path + HTTPErrorDirName + "/400.html";
            if (File.Exists(path))
                Hardcoded400Response = File.ReadAllBytes(path);
            path = Server.path + HTTPErrorDirName + "/404.html";
            if (File.Exists(path))
                Hardcoded404Response = File.ReadAllBytes(path);
            path = Server.path + HTTPErrorDirName + "/414.html";
            if (File.Exists(path))
                Hardcoded414Response = File.ReadAllBytes(path);
            path = Server.path + HTTPErrorDirName + "/500.html";
            if (File.Exists(path))
                Hardcoded500Response = File.ReadAllBytes(path);

        }

		private static bool LoadConfigFileIntoDictionary(string filepath, Dictionary<string, string> dict)
		{
			bool res = File.Exists(filepath);
			if(res)
			{
				byte[] filedata = File.ReadAllBytes(filepath);
				bool kvmode = true;
				byte[] k = new byte[128];
				byte[] v = new byte[128];
				int klength = 0;
				int j = 0;

				void InsertData()
				{
					dict[Encoding.UTF8.GetString(k, 0, klength)] = Encoding.UTF8.GetString(v, 0, j);
					k = new byte[128];
					v = new byte[128]; 
				}

				for(int i = 0; i < filedata.Length; i++)
				{
					if(filedata[i] > 31)
					{
						if(filedata[i] == (byte)'=')
						{
							if(filedata[i+1] == (byte)' ')
								i++;
							kvmode = false;
							klength = j;
							j = 0;
						}
						else
						{
							if(kvmode)
							{
								if(filedata[i] == (byte)' ')
									i++;
								k[j] = filedata[i];
							}
							else
							{
								v[j] = filedata[i];
							}
							j++;
						}
					}
					else
					{
						InsertData(); 
						kvmode = true;
						j = 0;
					}
				}
				InsertData();
			}
			return res;
		}

		private static string DetectFileExtension(string url)
		{
			for(int i = (url.Length - 1); i > 0; i--)
			{	
				if (url[i] == '.')
				{
					return url.Substring(i, url.Length - i);
				}
			}
			return ".";
		}

		private static string MIMETypeFromFileExtension(string fileExt)
		{
			string result = "*/*";
			MIMETypeList.TryGetValue(fileExt, out result);
			return result;
		}

        private static Func<HttpListenerContext, Task> HWS = null;

        private static void HandleRequest(IAsyncResult result)
        {
            Request reqObj = (Request)result.AsyncState;
            HttpListener listener = reqObj.listener;
            HttpListenerContext context = listener.EndGetContext(result); //This lets the server start listening for the next request.
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            reqObj.context = context;
            reqObj.state.recieved = true;

            if (request.IsWebSocketRequest) //WebSockets
            {
                reqObj.state.requestedWS = true;
                reqObj.state.finished = true;
                return;
            }

            Server.Print("Handling request on thread: " + System.Threading.Thread.CurrentThread.ManagedThreadId);
            string reqURL;
            string fileExt;
            string filepath;
            string method;
            byte[] buffer = { 0 };

            reqURL = request.Url.AbsolutePath;
            method = request.HttpMethod;
            Data.req = request;

            if (MaxConnectionsPerIP > -1)
            {
                string IPAddress = request.UserHostAddress;
                int numConnections;
                IPAddressConnections.TryGetValue(IPAddress, out numConnections);
                if (numConnections > MaxConnectionsPerIP)
                {
                    reqObj.state.finished = true;
                    response.StatusCode = 500;
                    response.Close();
                    return;
                }
            }

            if (!request.IsWebSocketRequest) //Regular HTTP requests
            {
                response.StatusCode = 200;
                try
                {
                    if (reqURL.Length <= URICharLimit) 
                    {
                        fileExt = DetectFileExtension(reqURL);
                        response.ContentType = MIMETypeFromFileExtension(fileExt);
                        //Route handling
                        if (fileExt[0] == '.' && fileExt.Length == 1)
                        {
                            filepath = myTryRoute(String.Join(null, reqURL, method.ToUpper()));
                            if (filepath[0] > 47)
                            {
                                buffer = FileCache.GetHTMLFileContent(filepath);
                            }
                            else if (filepath[0] == '&')
                            {
                                FileCache.GetHTTPErrorResponse(int.Parse(filepath.Substring(1)));
                            }
                            else if (filepath[0] == '#')
                            {
                                buffer = Encoding.UTF8.GetBytes(filepath.Substring(1, filepath.Length - 1));
                            }
                        }
                        //Fetching static files
                        else if (fileExt.Length > 0)
                        {
                            string dir = GetFileExtDir(fileExt);
                            filepath = String.Join(null, path, dir, "/", reqURL.Substring(1, reqURL.Length - 1));
                            buffer = FileCache.TryGetFile(filepath);
                        }
                    }
                    else
                    {
                        Server.Print("414 - URI too long");
                        response.StatusCode = 414;
                        buffer = FileCache.GetHTTPErrorResponse(414);
                    }
                }
                catch (Exception e)
                {
                    Server.PrintException(e);
                    response.StatusCode = 500;
                    buffer = FileCache.GetHTTPErrorResponse(500);
                }

                response.ContentLength64 = buffer.Length;
                System.IO.Stream output = response.OutputStream;
                try
                {
                    output.Write(buffer, 0, buffer.Length);
                }
                catch (Exception e)
                {
                    Server.PrintException(e);
                }
                output.Close();

            }
            reqObj.state.finished = true;
            Routes.ClearThreadStatics();
            Server.ClearThreadStatics();
            Data.ClearThreadStatics();
		}

		private static class FileCache
		{
			public class LRUNode
			{
				public string key;
				public LRUNode prev;
				public LRUNode next;
				public LRUNode(string key, LRUNode next, LRUNode prev)
				{
					this.key = key;
					this.next = next;
					this.prev = prev;
				}
			}
			public class FileData
			{
				public LRUNode node;
				public byte[] data;
				public DateTime timeCached;
				public FileData(byte[] data, LRUNode node, DateTime timeCached)
				{
					this.data = data;
					this.node = node;
					this.timeCached = timeCached;
				}
			}
			private static LRUNode head;
			private static LRUNode tail;
			private static int filecount = 0;
			private static ConcurrentDictionary<string, FileData> filecache = new ConcurrentDictionary<string, FileData>();
			private static readonly object FileCacheLock = new Object();

			public static byte[] GetHTMLFileContent(string filename)
			{
				byte[] res = TryGetFile(String.Join(null, Server.path, Server.HTMLDirName, "/", filename));
				res = ProcessTemplate(Server._TemplateData, res);
				Server._TemplateData = null;
				if(res == null)
				{
					return GetHTTPErrorResponse(404);
				}
				return res;
			}

			public static byte[] GetHTTPErrorResponse(int code)
			{
                switch (code)
                {
                    case 400:
                        return Hardcoded400Response;
                    case 404:
                        return Hardcoded404Response;
                    case 414:
                        return Hardcoded414Response;
                    case 500:
                        return Hardcoded500Response;
                    default:
                        return GenericErrorResponse;
                }
			}

			public static byte[] TryGetFile(string filepath)
			{
				FileData file;
				if(filecache.ContainsKey(filepath))
				{
					try
					{
						file = filecache[filepath];
						//cache is up to date
						if(DateTime.Compare(file.timeCached, File.GetLastWriteTime(filepath)) > 0)
						{
							Task.Run(() => { lock(FileCacheLock){ UpdateLRU(filepath, file, true); } });
							return file.data;
						}
						//cache isn't up to date, fall through to grab it off the disk.
					}
					catch(Exception e)
					{	
						Server.PrintException(e);
						//probably don't need to do anything except alert for the exception.
						//We'll fall through and try to get the file off disk.
					}
				}

				//Everything fell through so we try the disk now.
				byte[] res = GetFileFromDisk(filepath);
				if(res == null)
				{
					res = GetHTTPErrorResponse(404);
				}
				return res;
			}

			private static byte[] GetFileFromDisk(string filepath)
			{
				byte[] data;
				try
				{
					if(File.Exists(filepath))
					{
						data = File.ReadAllBytes(filepath);
						Server.Print("'{0}' found on disk.", filepath);

						//asynchronously update the cache. The response thread won't wait for it because it already got the file.
						Task.Run(() => UpdateCache(filepath, data));
						return data;
					}
					else
					{
						Server.Print("'{0}' not found.", filepath);
						return null;
					}
				}
				catch(Exception e)
				{
					Server.PrintException(e);
					return GetHTTPErrorResponse(500);
				}
			}

			private static void UpdateCache(string filepath, byte[] data)
			{
				FileData pendingData = new FileData(data, new LRUNode(filepath, null, null), DateTime.Now);
				lock(FileCacheLock)
				{
					filecache[filepath] = pendingData;
					UpdateLRU(filepath, pendingData, true);
				}
			}

			private static void UpdateLRU(string filepath, FileData file, bool newfile)
			{
				FileData trash;
				LRUNode phead = head;
				LRUNode ptail = tail;

				if(newfile)
				{
					head = file.node;
					if(phead != null)
						phead.next = head;

					if(filecount < Server.FileCache_FileLimit)
					{
						filecount++;
					}
					else
					{
						tail = ptail.next;
						tail.prev = null;
						filecache.TryRemove(ptail.key, out trash);
					}
				}
				else
				{
					LRUNode pprev = file.node.prev;
					LRUNode pnext = file.node.next;
					Console.WriteLine("pprev: {0}", pprev);
					Console.WriteLine("pnext: {0}", pnext);
					if(pprev != null) pprev.next = pnext;
					if(pnext != null) pnext.prev = pprev;
					head = file.node;
					head.prev = phead;
					head.next = null;
					phead.next = head;
				}
			}

			public static byte[] ProcessTemplate(object data, byte[] template)
			{
				if(Server.Templating == false)
				{
					data = null;
					Server.Print("Warning: You passed template data but templating is not enabled. Enable in the configuration file.");
				}
				if(data == null)
					data = new {};
				
				PropertyInfo[] fi = data.GetType().GetProperties();
				PropertyInfo prop;
				bool inHTMLTag = false;
				StringBuilder res = new StringBuilder(template.Length + fi.Length*20, Server.ProcessedTemplateMaxSize);
				char[] currentVar = new char[32];
				string completeVar;
				int currentVarIndex = 0;

				for(int i = 0; i < template.Length; i++)
				{
					try
					{
						if(!inHTMLTag && template[i] == '<'
							&& Char.ToUpper((char)template[i+1]) == 'H'
							&& Char.ToUpper((char)template[i+2]) == 'T'
							&& Char.ToUpper((char)template[i+3]) == 'M'
							&& Char.ToUpper((char)template[i+4]) == 'L'
							&& template[i+5] == '>')
						{
							inHTMLTag = true;
							res.Append((char)template[i]);
							res.Append((char)template[i+1]);
							res.Append((char)template[i+2]);
							res.Append((char)template[i+3]);
							res.Append((char)template[i+4]);
							res.Append((char)template[i+5]);
							i+=6;
						}
						else if(template[i] == '{' && template[i+1] == '{')
						{
							i+=2;
							currentVarIndex = i;
							while(isValidVarChar((char)template[i]) && (i - currentVarIndex < 32))
							{
								currentVar[i - currentVarIndex] = (char)template[i];
								i++;
							}
							completeVar = new string(currentVar);
							if(template[i] != '}' && template[i+1] != '}')
							{
								throw new Exception("Shuriken: Variable tag for variable `" + completeVar + "` improperly closed or variable contained invalid characters. Processing template has been aborted.");
							}
							else
							{
								i+=2;
								prop = data.GetType().GetProperty(completeVar);
								if(prop != null)
								{
									res.Append(prop.GetValue(data, null));
								}
								else
								{
									Server.Print("Shuriken: Variable `{0}` was not passed to template.", completeVar);
								}
							}
						}
						res.Append((char)template[i]);
					}
					catch(Exception e)
					{
						Server.Print("Shuriken: There was an exception while processing an HTML template.\n{0}", e);
						return template;
					}
				}
				return Encoding.ASCII.GetBytes(res.ToString());

                bool isValidVarChar(char ch)
                {
                    return ((ch > 47 && ch < 58) || (ch > 64 && ch < 91) || (ch == '_') || (ch > 96 && ch < 122));
                }
            }
		}

	}//end Server
}