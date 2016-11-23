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

	public static class Server
	{
		private static string path = Environment.CurrentDirectory + "/";
		private static string staticDirName = "static";
		private static string htmlDirName = "html";
		private static int uriCharLimit = 255;
		private static bool started = false;
		private static bool showServerMsgs = true;
		private static bool showExceptions = true;
		private static int ProcessedTemplateMaxSize = 5242880;
		private static int FileCache_FileLimit = 1000;
		private static bool templating = false;

		//Number of requests being processed.
		private static int _CurrentRequests = 0;
		public static int CurrentRequests
		{
			get { return _CurrentRequests; }
		}
		public static int HighTrafficRequestThreshold = 20;

		public static void Start(int port = 5000)
		{
			if(!started)
			{
				started = true;
				MIMETypeList.Add(".html", "text/html");
				MIMETypeList.Add(".htm", "text/html");
				MIMETypeList.Add(".css", "text/css");
				MIMETypeList.Add(".png", "image/png");
				MIMETypeList.Add(".jpg", "image/jpeg");
				MIMETypeList.Add(".bmp", "image/bmp");

				HttpListener listener = new HttpListener();
				listener.Start();
				listener.Prefixes.Add("http://*:" + port.ToString() + "/");
				Routes.init_SendServerSharedItems();

				//Start a thread to listen for incoming requests.
				Thread serverThread = new Thread(delegate()
	            {
					while(true)
					{
						IAsyncResult result = listener.BeginGetContext(new AsyncCallback(HandleRequest),listener);

						Server.Print("Awaiting request.");
						result.AsyncWaitHandle.WaitOne();
						Server._CurrentRequests++;
					}
	            });
	    		serverThread.Start();

	    		//A thread which accepts console input without blocking.
	    		Thread consoleInputThread = new Thread(delegate()
	            {
	            	string input;
	            	string[] cmds;
	            	
					while(true)
					{
	    				input = Console.ReadLine();
	    				cmds = input.Split(' ');

	    				Func<string, int, bool> CheckCommand = (s, i) => (cmds.Length > i && cmds[i] == s);
						
	    				if(CheckCommand("messages", 1))
	    				{
	    					if(CheckCommand("enable", 0))
	    					{
	    						showServerMsgs = true;
	    						Console.WriteLine("Server Messages Enabled.");
	    						continue;
	    					}
	    					else if(CheckCommand("disable", 0))
	    					{
	    						showServerMsgs = false;
	    						Console.WriteLine("Server Messages Disabled.");
	    						continue;
	    					}
	    				}
	    				Console.WriteLine("Shuriken: Command \"" + input + "\" not recognized.");
	    			}
				});
				consoleInputThread.Start();
			}
		}

		public static void VariableTemplating(bool enable = true)
		{
			Server.templating = enable;
		}

		public static void Print(string msg, object param1 = null, object param2 = null, object param3 = null, object param4 = null)
		{
			if(showServerMsgs)
				Console.WriteLine(msg, param1, param2, param3, param4);
		}

		public static void PrintException(Exception e)
		{
			if(showExceptions)
				Console.WriteLine(e);
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

		private static string DetectFileExtension(string url)
		{
			for(int i = (url.Length - 1); i > 0; i--)
			{	
				if (url[i] == '.')
				{
					return url.Substring(i, (byte)url.Length - i);
				}
			}
			return ".";
		}

		private static Dictionary<string, string> MIMETypeList = new Dictionary<string, string>();

		private static string MIMETypeFromFileExtension(string fileExt)
		{
			string result = "*/*";
			MIMETypeList.TryGetValue(fileExt, out result);
			return result;
		}

		private static void HandleRequest(IAsyncResult result)
		{
			string reqURL;
			string fileExt;
			string filepath;
			string method;
			byte[] buffer = {0};

			HttpListener listener = (HttpListener) result.AsyncState;
			HttpListenerContext context = listener.EndGetContext(result);
			HttpListenerRequest request = context.Request;
			HttpListenerResponse response = context.Response;

			reqURL = request.Url.AbsolutePath;
    		method = request.HttpMethod;
    		Data.req = request;


			Server.Print("Thread {0} handling a request.", Thread.CurrentThread.ManagedThreadId);
			Server.Print("Currently {0} requests being processed.", Server.CurrentRequests);

			try{
    		
				if(reqURL.Length <= uriCharLimit)
				{
					fileExt = DetectFileExtension(reqURL);
					response.ContentType = MIMETypeFromFileExtension(fileExt);
					//Route handling
					if(fileExt[0] == '.' && fileExt.Length == 1)
					{	
						filepath = myTryRoute(reqURL + method.ToUpper());
	    				Server.Print("Route: {0} on Thread: {1}", reqURL, Thread.CurrentThread.ManagedThreadId);
						
						if(filepath[0] != '#')
						{
							buffer = FileCache.GetHTMLFileContent(filepath);
						}
						else
							buffer = System.Text.Encoding.UTF8.GetBytes(filepath.Substring(1, filepath.Length - 1));
					}
					//Fetching static files
					else if (fileExt.Length > 0)
					{
						filepath = path + staticDirName + "/" + reqURL.Substring(1, reqURL.Length-1);
						buffer = FileCache.GetFileContent(filepath);
					}
				}
				else
				{
					Server.Print("414 - URI too long");
					buffer = System.Text.Encoding.UTF8.GetBytes("<HTML><BODY>Error Code 414: Request-URI Too Long.</BODY></HTML>");
				}
			}
			catch(Exception e)
			{
				Server.PrintException(e);
				Server.Print("------------------\nShuriken caught the above exception and is still running. However, your code is probably not functioning as intended.");
				buffer = System.Text.Encoding.UTF8.GetBytes("<HTML><BODY>500 Internal Server Error.</BODY></HTML>");
			}
			response.ContentLength64 = buffer.Length;
			System.IO.Stream output = response.OutputStream;
			output.Write(buffer,0,buffer.Length);
			output.Close();
			Server.Print("Closing output stream (Thread {0})", Thread.CurrentThread.ManagedThreadId);

			Data.ClearThreadStatics();
			Server._CurrentRequests--;
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
				public FileData(byte[] data, LRUNode node)
				{
					this.data = data;
					this.node = node;
				}
			}
			private static LRUNode head;
			private static LRUNode tail;
			private static int filecount = 0;
			private static Dictionary<string, FileData> filecache = new Dictionary<string, FileData>();
			private static object FileCacheLock = new Object();
			private static int ThreadsWaitingForCache = 0;

			public static byte[] GetHTMLFileContent(string filename)
			{
				byte[] res = GetFileContent(Server.path + Server.htmlDirName + "/" + filename);
				res = ProcessTemplate(Server._TemplateData, res);
				Server._TemplateData = null;
				return res;
			}
			public static byte[] GetFileContent(string filepath)
			{
				byte[] result = null;
				FileData cacheData;
				LRUNode node;

				//Grab the file off disk if there is currently low traffic in order to keep the cache updated relatively responsively.
				//Alternatively, if the cache has a lot of threads waiting, just go for the file on disk.
				if(Server.CurrentRequests < Server.HighTrafficRequestThreshold || ThreadsWaitingForCache > 9)
				{
					string reason;
					if(Server.CurrentRequests < Server.HighTrafficRequestThreshold)
						reason = "low request traffic.";
					else
						reason = " too many threads currently waiting for cache access.";
					Server.Print("Skipping cache due to " + reason);
					Server.Print("'{0}' found on disk.", filepath);
					return File.ReadAllBytes(filepath);
				}

				ThreadsWaitingForCache++;
				lock(FileCacheLock)
				{	
					ThreadsWaitingForCache--;
					//Caching
					if(filecache.TryGetValue(filepath, out cacheData) == true)
					{
						Server.Print("'{0}' found in cache.", filepath);
						result = cacheData.data;
						UpdateLRU(filepath, cacheData, false);
					}
					else if(File.Exists(filepath))
					{
						Server.Print("'{0}' found on disk.", filepath);
						result = File.ReadAllBytes(filepath);
						node = new LRUNode(filepath, null, null);
						cacheData = new FileData(result, node);
						filecache[filepath] = cacheData;
						UpdateLRU(filepath, cacheData, true);
					}
					else
					{
						Server.Print("'{0}' not found. (404)", filepath);
						return System.Text.Encoding.UTF8.GetBytes("<HTML><BODY>Error code 404: File not found.</BODY></HTML>");
					}
				}
				return result;
			}

			private static void UpdateLRU(string filepath, FileData file, bool newfile)
			{
				LRUNode phead = head;
				LRUNode ptail = tail;

				if(newfile == true)
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
						filecache.Remove(ptail.key);
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
				if(Server.templating == false)
				{
					data = null;
					Server.Print("Warning: You passed template data but templating is not enabled. Call Shuriken.Server.VariableTemplating(true) to enable.");
				}
				if(data == null)
					return template;
				
				PropertyInfo[] fi = data.GetType().GetProperties();
				PropertyInfo prop;
				bool inHTMLTag = false;
				//Attempt at being smart about the string builder's size.
				//I'm starting it out with the template's size plus 20 times the number of variables being passed to the template.
				//This way if there are any strings being passed it might be big enough that it doesn't need to grow.
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
				
			}

			private static bool isValidVarChar(char ch)
			{
				return ((ch > 47 && ch < 58) || (ch > 64 && ch < 91) || (ch == '_') || (ch > 96 && ch < 122));
			}
		}

	}
}