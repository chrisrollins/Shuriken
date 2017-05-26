/* 
This is a web browser websockets library for use with a Shuriken webserver.
*/

/*event format
event types:
numbered: data is prefixed with a null terminator (0 value) followed by the number of the event as the next byte.
named: data is prefixed with a string which identifies the event. this identifier must be null terminated.

examples:
numbered event: 0 => numbered event, number 0
numbered event: 1 => numbered event, number 1
numbered event: 12 => numbered event, number 12
numbered event: 100 => numbered event, number 100

named event: eatFood => named event, "eatFood"
named event: 2good => named event, "2good"

when registering a numbered event, remove leading 0s

dispatching uses an array
*/

Shuriken.WebSockets = Object.freeze(

	(function(){
		const WSDataSize = 2048;
		const WSHeaderSize = 256;
		const namedEvents = {};
		const numberedEvents = new Array(256);
		const socket = new WebSocket(`ws://${document.location.host}`);

		const parseEvent = function(str){
			const result = {callback: undefined, data: undefined, error: undefined};

			if(str[0] === "\0") //if the first character is a null terminator this is a numbered event.
			{
				result.callback = numberedEvents[str.charCodeAt(1)];
				
				let i = 2;
				while(i < str.length && str[i] !== "\0")
				{
					i++;
				}
				
				result.data = str.slice(2, i);
			}
			else //named event
			{
				let i = 0;
				while(str[i + 1] !== "\0")
				{
					i++;
					if(i >= WSHeaderSize)
					{
						result.error = "Invalid Event name - This event name was not null terminated. Event names need to be null terminated.";
						break;
					}
				}
				result.callback = namedEvents[str.slice(0, i)];
				result.data = str.slice(i);
			}

			return result;
		};

		socket.addEventListener("message", function(e)
		{
			const reader = new FileReader();
			reader.addEventListener("loadend", function(e)
			{
				const incomingEvent = parseEvent(e.target.result);

				if(incomingEvent.error)
				{
					console.error(incomingEvent.error);
					console.trace();
				}

				if(typeof incomingEvent.callback === "function")
				{
					incomingEvent.callback(incomingEvent.error, incomingEvent.data);
				}
				else
				{
					console.error("callback is not a function.");
					console.trace();
				}
			})
			reader.readAsText(e.data);
		});
		
		return {
			listen: {
				namedEvent: function(name, callback)
				{
					if(namedEvents[name])
					{
						console.warn(`Named event "${name}" already existed. You have overwritten it.`);
						console.trace();
					}
					namedEvents[name] = callback;
				},

				numberedEvent: function(number, callback)
				{
					if(number < 0 || number > 255)
					{
						console.error(`Event number (${number}) out of range. Acceptable range: 0-255`);
						console.trace();
					}
					else
					{

						if(numberedEvents[number])
						{
							console.warn(`Numbered event (${number}) already existed. You have overwritten it.`);
							console.trace();
						}
						numberedEvents[number] = callback;
					}
				}
			},
			send: {
				namedEvent: function(name, data)
				{

			console.log("asdf",data.charCodeAt(data.length-1));
					if(name.length < WSHeaderSize)
					{
						socket.send(`${name}\0${data}`);
					}
					else
					{
						console.error(`Could not send WebSocket message. Event name can't be longer than ${WSHeaderSize} characters.`);
						console.trace();
					}
					
				},

				numberedEvent: function(number, data)
				{
					if(number < 256)
					{
						socket.send(`\0${String.fromCharCode(number)}${data}`);
					}
					else
					{
						console.error(`Could not send WebSocket message. Event number (${number}) out of range. Acceptable range: 0-255`);
						console.trace();
					}
				}
			}
		};
	})()
);