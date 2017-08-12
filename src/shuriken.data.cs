// Shuriken - Webserver Micro-Framework for C#
// Created by Christopher Rollins


// /////////////////////////////////////////////

// Copyright (c) 2016 Christopher Rollins

// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

////////////////////////////////////////////

/*
The Data class provides functions related to data in the HTTP header such as Post Data and URL parameters.
You can also directly access the HTTP Request Object (HttpListenerRequest native to .NET) with the property Shuriken.Data.req
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

	public static class Data
	{	
		[ThreadStatic] private static string PostData;
		[ThreadStatic] private static Dictionary<string, string> PostDataDict;
		[ThreadStatic] public static HttpListenerRequest req;
        
		public static string GetURLParam(string paramName)
		{
			return req.QueryString[paramName];
		}

		public static string GetFormField(string fieldName)
		{
			string raw = GetRawPostData();
			string[] pairs = raw.Split('&');
			string[] temp;
			string result = "";
			if(Data.PostDataDict == null)
			{
				Data.PostDataDict = new Dictionary<string, string>();
				foreach(string pair in pairs)
				{
					temp = pair.Split('=');
					Data.PostDataDict.Add(temp[0], temp[1]);
				}
			}
			Data.PostDataDict.TryGetValue(fieldName, out result);
			return result;
		}

		public static string GetRawPostData()
		{
			if (Data.PostData == null)
			{
				string text;
				using (StreamReader reader = new StreamReader(req.InputStream, req.ContentEncoding))
				{
				    text = reader.ReadToEnd();
				}
				Data.PostData = text;
			}
			return Data.PostData;
		}
		
		public static void ClearThreadStatics()
		{
			PostData = null;
			PostDataDict = null;
			req = null;
		}
	}
}