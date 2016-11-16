# Shuriken
## C# web server microframework by Chris Rollins

::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::

Copyright (c) 2016 Christopher Rollins

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::

Shuriken is primarily a learning project for myself. It provides most of the basic functionality for a web server. It was inspired by Flask (for Python) and Express (for Node). The goal is to make it very simple to use so that even new web developers can use it.

Shuriken has no 3rd party dependencies.

Shuriken is "unopinionated", meaning that it does not enforce any particular organization pattern for the program, nor does it provide custom types. Instead, it provides its functionality through static methods.

# Usage

## Default system paths:

### /static
Static files available on direct request, such as images.

### /html
HTML files served by routes.


## Methods:

`void Shuriken.Server.Start(int port)`
- Starts listening for requests. port is 5000 by default.

`void Shuriken.Routes.Add(string route, string method, string filename, Action f)`
- Creates a route for serving a webpage or an AJAX request. (note that you will need a 3rd party JSON serializer)

Parameters:
- _route_: The URL route.
- _method_: The HTTP method associated with this route.
- _filename_: The HTML or template file to serve for this route.
- _f_: A custom function (void) defined by the programmer which will run before the route is served. This function should call one of the following functions: `Shuriken.Routes.Render`, `Shuriken.Routes.Redirect`, or `Shuriken.Routes.SendData` after it finishes.
- _filename_ and _f_ are optional, but one of the two is required.


`void Shuriken.Routes.Render(object TemplateData)`
- This should be called at the end of your custom route function if you want to render the HTML page associated with the route.
- _TemplateData_ allows you to pass variables to the HTML file.
- Currently only variables are supported. Variables are enclosed in double curly braces like this: _{{x}}_.
- When you pass template data you must pass an object with corresponding properties, which can be of any type, to the template variables.
- For example, if the template has _{{x}}_ and _{{y}}_, the object should look something like this: _{x = 2, y = "foo"}_
- You can simply pass an anonymous class like so: `Shuriken.Routes.Render(new {x = 2, y = "foo"});`
- _TemplateData_ is optional.
