using System;
using Shuriken;

/*
This is an example webserver using Shuriken
To run:
1. Put this file and the Shuriken source files in the same directory.
2. Compile these files into an executable.
3. Run the resulting executable.
*/

namespace MyServer
{
	public static class Program
	{
		public static void Main()
		{
			//Define the routes
			//The last argument is an optional callback which can be used as a controller.
			Shuriken.Routes.Add("/", "GET", "index.html", Welcome);
			Shuriken.Routes.Add("/name", "POST", SubmitName);

			//Start the server
			Shuriken.Server.Start();
		}

		public static void SubmitName()
		{
			//Set some data in the Routes.RedirectData property and then redirect.
			Shuriken.Routes.RedirectData = new {name = Shuriken.Data.GetFormField("name")};
			Shuriken.Server.Print(Shuriken.Data.GetFormField("name"));
			Shuriken.Routes.Redirect("/");
		}

		public static void Welcome()
		{
			//If redirected from another route, Routes.RedirectData can hold some data.
			Shuriken.Routes.Render(Shuriken.Routes.RedirectData);
		}
	}
}