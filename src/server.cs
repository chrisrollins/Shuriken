using System;
using Shuriken;

namespace MyServer
{
	public static class Program
	{
		public static void Main()
		{
			//route with a custom function which will send some data instead of rendering a page
			Shuriken.Routes.Add("/rawdata", "GET", senddata);

			//route with broken code. Shuriken needs to tell the programmer something went wrong.
			Shuriken.Routes.Add("/brokenroute", "GET", badrender);

			//route with broken code. Shuriken needs to tell the programmer something went wrong.
			Shuriken.Routes.Add("/exception", "GET", exc);

			//route without a custom function which will just serve a static html page
			Shuriken.Routes.Add("/", "GET", "index.html", welcome);

			//route with only html, no custom function
			Shuriken.Routes.Add("/form", "GET", "form.html");

			//route with only html, no custom function
			Shuriken.Routes.Add("/portfolio", "GET", "portfolio.html");

			//POST route. raw POST data is available by calling Shuriken.Data.GetPostData() which returns a string.
			Shuriken.Routes.Add("/form", "POST", processForm);

			//Bad redirect
			Shuriken.Routes.Add("/badredir", "GET", badredir);

			//route using an anonymous function.
			Shuriken.Routes.Add("/login", "GET", "user.html", delegate() {
				bool loggedin=false; //imagine you have some code with a database
				if(loggedin)
				{
					//I don't have this route at all so this would break if it came into this block.
					//However, this is an example of the structure that you might want.
					Shuriken.Routes.Redirect("/user");
				}
				
				Shuriken.Routes.Redirect("/");
				
			});

			//Starting the server. This is non-blocking so you can write code below it if you want.
			Shuriken.Server.Start();
		}

		//This user function uses Render without an html file specified, resulting in a 500 internal server error.
		public static void badrender()
		{
			Shuriken.Routes.Render();
		}

		//This user function throws an error. Shuriken needs to catch this.
		public static void exc()
		{
			int[] derp = new int[1];
			derp[3] = 0;
			Shuriken.Routes.Render();
		}

		public static void welcome()
		{
			//This is the structure of the function you pass to Routes.Add
			//The html file you passed to Routes.Add will be rendered if your function returns Routes.Render()
			//Return false on failed validation or something like that.
			//The function and filename are optional.

			Console.WriteLine("URL Param 'name': {0}", Shuriken.Data.GetURLParam("name"));
			Shuriken.Routes.Render(new {x = "This text was added to the template dynamically on the serverside!"});
		}

		public static void senddata()
		{
			//This sends the string passed to it as the response instead of sending a page.
			Shuriken.Routes.SendData("{key: value, key2: value}");
		}

		public static void processForm()
		{
			string rawPostData = Shuriken.Data.GetRawPostData();
			Console.WriteLine("Raw Post Data: {0}", rawPostData);
			Console.WriteLine("text: {0}", Shuriken.Data.GetFormField("text"));
			Shuriken.Routes.Redirect("/", "GET");
		}

		public static void badredir()
		{
			Shuriken.Routes.Redirect("/a", "GET");
		}
	}
}