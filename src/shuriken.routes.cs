// Shuriken - Webserver Micro-Framework for C#
// Created by Christopher Rollins


// /////////////////////////////////////////////

// Copyright (c) 2016 Christopher Rollins

// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

////////////////////////////////////////////

/*
The Routes class provides functions for creating routes and an easy way to run code for route responses.
Call Shuriken.Routes.Add to add a route.
If you provide it with a custom function as a parameter your function must call one of the following:
Shuriken.Routes.Render
Shuriken.Routes.Redirect
Shuriken.Routes.SendData
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

	public static class Routes
	{
		private static Dictionary<string, routeData> routeList = new Dictionary<string, routeData>();
		
		private struct routeData
		{
			public string filename;
			public Action fn;
		}

		[ThreadStatic] private static string CustomFnReturn;
		[ThreadStatic] private static bool RenderPathChosen = false;
		private static bool init_done = false;

		public static void Add(string route, string method, string filename, Action f)
		{
			routeData data = new routeData();
			method = method.ToUpper();
			data.filename = filename;
			data.fn = f;
			routeList.Add(route + method, data);
		}

		public static void Add(string route, string method, string filename)
		{
			Routes.Add(route, method, filename, delegate() {Routes.CustomFnReturn = null;});
		}

		public static void Add(string route, string method, Action f)
		{
			Routes.Add(route, method, "", f);
		}

		//return from the custom route function to redirect to another route
		//Defaults to GET method
		public static void Redirect(string route, string method = "GET")
		{
			RenderPath(route + method);
		}

		//return from the custom route function to render the route
		public static void Render(object TemplateData = null)
		{
			string res = null;
			Server._TemplateData = TemplateData;
			RenderPath(res);
		}

		//return from the custom route function to send an arbitrary string as the response instead of a file
		public static void SendData(string data)
		{
			RenderPath("#" + data);
		}

		public static void init_SendServerSharedItems()
		{
			if(!init_done)
			{
				init_done = true;
				Server.init_myTryRoute(TryRoute);
			}
		}


		//private methods

		private static void RenderPath(string renderpath)
		{
			if(!RenderPathChosen)
			{
				RenderPathChosen = true;
				Routes.CustomFnReturn = renderpath;
			}
			else
				throw new System.InvalidOperationException("Multiple routing methods called.");
		}

		private static string TryRoute(string route)
		{
			routeData data;

			if(routeList.TryGetValue(route, out data) == true)
			{
				try
	    		{
	        		data.fn();
	    		}
	    		catch (Exception e)
	    		{
	    			Server.Print("Shuriken: An error in user function occurred.");
	    			Server.PrintException(e);
	    			Routes.CustomFnReturn = null;
	    		}
	    		
				if(Routes.CustomFnReturn != null)
				{
					route = Routes.CustomFnReturn;
					if(route[0] != '#')
					{
						Server.Print("redirect to {0}", route);
						return TryRoute(route);
					}
					else
					{
						return route;
					}
				}

				if(data.filename == "")
				{
					Server.Print("Shuriken: There was a problem with user custom function for route\n{0}. Best guess: Render() was used without an html file specified.", route);
					return "#<HTML><BODY>500 Internal Server Error.</BODY></HTML>";
				}

				return data.filename;
			}
			Thread.Sleep(5000);
			return "#<HTML><BODY>Error code 400: Bad request.</BODY></HTML>";
		}

	}
}