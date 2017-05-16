using System;
using Shuriken;

namespace MyServer
{
	public static class WSFunctions
	{
		public static async void test(byte[] data)
		{
			Console.WriteLine("Recieved data:");
			Console.WriteLine(System.Text.Encoding.UTF8.GetString(data));
			await Shuriken.WebSockets.SendEvent(0, System.Text.Encoding.ASCII.GetBytes("Hello!"));
		}
	}

	public static class Program
	{
		public static void Main()
		{
			Shuriken.Routes.Add("/", "GET", "index.html");

			Shuriken.WebSockets.Enable();
			Shuriken.WebSockets.FastEvent(0, WSFunctions.test);

			Shuriken.Server.SetFileExtensionDirectory("js", ".js");

			Shuriken.Server.Start();
		}
	}
}