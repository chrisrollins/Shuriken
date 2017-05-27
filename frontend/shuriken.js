const Shuriken = (function()
{
	const frameworkName = "Shuriken";
	const nameBindings = {};
	const keysInitialized = {};
	let ShurikenElementCount = 0;
	let windowLoaded = false;
	const API = {
		Data: {}, 
		WebSockets: {}
	};

	return (function(){
		//SETUP
		const dataStorage = function()
		{
			if(!sessionStorage._ShurikenSession)
			{
				sessionStorage._ShurikenSession = "{}";
			}
			const _data = JSON.parse(sessionStorage._ShurikenSession);

			return Object.freeze(
			{
				set: function(name, value)
				{
					_data[name] = value;
					sessionStorage._ShurikenSession = JSON.stringify(_data);
					if(Array.isArray(value))
					{
						value.push = function(pushedVal)
						{
							Array.prototype.push.call(value, pushedVal);
							API_set(name, value);
						}
					}
				},
				get: function(name, value)
				{
					return _data[name];
				},
				get all()
				{
					return _data;
				}
			});
		}();

		for(const name in dataStorage.all)
		{
			if(!keysInitialized[name])
			{
				API_init(name, dataStorage.get(name));
			}
		}
		
		(function()
		{
			for(const event of ["onabort", "onafterprint", "onanimationcancel", "onanimationend", "onanimationiteration", "onappinstalled", "onauxclick", "onbeforeinstallprompt", "onbeforeprint", "onbeforeunload", "onblur", "onchange", "onclick", "onclose", "oncontextmenu", "ondblclick", "ondevicelight", "ondevicemotion", "ondeviceorientation", "ondeviceorientationabsolute", "ondeviceproximity", "ondragdrop", "onerror", "onfocus", "ongotpointercapture", "onhashchange", "oninput", "onkeydown", "onkeypress", "onkeyup", "onlanguagechange", "onload", "onloadend", "onloadstart", "onlostpointercapture", "onmousedown", "onmousemove", "onmouseout", "onmouseover", "onmouseup", "onmozbeforepaint", "onpaint", "onpointercancel", "onpointerdown", "onpointerenter", "onpointerleave", "onpointermove", "onpointerout", "onpointerover", "onpointerup", "onpopstate", "onrejectionhandled", "onreset", "onresize", "onscroll", "onselect", "onselectionchange", "onselectstart", "onstorage", "onsubmit", "ontouchcancel", "ontouchmove", "ontouchstart", "ontransitioncancel", "ontransitionend", "onunhandledrejection", "onunload", "onuserproximity", "onvrdisplayconnected", "onvrdisplaydisconnected", "onvrdisplaypresentchange"])
			{
				const funcs = [];

				if(window[event] !== undefined && window[event] !== null)
				{
					if(typeof window[event] === "function")
					{
						funcs.push(window[event]);
						console.warn(`${frameworkName}: There was already a function in window.${event}. It has been included in the ${frameworkName} event dispatcher's function list. It will still be called when window.${event} triggers.`);
					}
					else
					{
						console.warn(`${frameworkName}: There was an event conflict with window.${event}. There was a non-function value in the event. It has been overwritten by the ${frameworkName} event dispatcher.`);
					}
				}

				window[event] = function(e)
				{
					let result;
					funcs.forEach(function(f)
					{
						const fres = f(e);
						if(fres !== undefined)
						{
							result = fres;
						}
					});

					if(result !== undefined)
					{
						return result;
					}
				};

				Object.defineProperty(window, event,
				{
					set: function(f)
					{
						funcs.push(f);
					}
				});
			}

		})();

		window.onload = function()
		{
			windowLoaded = true;
		}
		//END SETUP

		//INTERNAL FUNCTIONS
		function generateElement(elementType)
		{
			const el = document.createElement(elementType);
			el.ShurikenID = ShurikenElementCount++;
			el.ShurikenCleared = true;
			return el;
		}

		function clearElement(element)
		{
			if(!element.ShurikenCleared)
			{
				const [...children] = element.childNodes;
				for(const child of children)
				{
					if(child.ShurikenID !== undefined)
					{
						child.remove();
					}
				}
				element.ShurikenCleared = true;
			}
		}

		function refreshElement(element)
		{
			updateDispatch(element, dataStorage.get(element.ShurikenNameBind));
		}

		function refreshName(name)
		{
			for(const element of (nameBindings[name] || []))
			{
				updateDispatch(element, dataStorage.get(name));
			}
		}

		function updateDispatch(element, data)
		{
			clearElement(element);
			const defaultFunc = function()
			{
				if(data === undefined || data === null)
				{
					console.warn(`${frameworkName}: The data bound to ${element} with id '${element.id}' is undefined.`);
					return;
				}
				else if(Array.isArray(data))
				{
					const table = generateElement("table");
					updateDispatch(table, data);
					element.appendChild(table);
				}
				else
				{
					if(element.value)
					{
						element.value = data;
					}
					else
					{
						element.innerText = data;
					}
				}

			}

			function lists()
			{
				if(Array.isArray(data))
				{
					const fragment = document.createDocumentFragment();
					for(const item of data)
					{
						const li = generateElement("li");
						if(typeof item === "object")
						{
							updateDispatch(li, item);
						}
						else
						{
							li.innerText = item;
						}
						fragment.appendChild(li);
					}

					element.appendChild(fragment);
				}
				else
				{
					defaultFunc();
				}
			}

			function tables()
			{
				if(typeof data === "object")
				{
					const fragment = document.createDocumentFragment();
					if(element.nodeName === "TABLE")
					{
						for(const child of element.childNodes)
						{
							if(child.nodeName === "TBODY")
							{
								element = child;
								break;
							}
						}
					}
					if(Array.isArray(data))
					{
						let atLeastTwoDimensions = true;
						for(let i = 0; i < data.length; i++)
						{
							if(!Array.isArray(data[i]))
							{
								atLeastTwoDimensions = false;
								break;
							}
						}
						const outer = (atLeastTwoDimensions)?data:[data];
						for(const arr of outer)
						{
							const tr = generateElement("tr");
							const inner = (Array.isArray(arr))?arr:[arr];
							for(const item of inner)
							{
								const td = generateElement("td")
								updateDispatch(td, item);
								tr.appendChild(td);
							}
							fragment.appendChild(tr);
						}
					}
					else
					{
						for(let i = 2; i > 0; i--)
						{
							for(const key in data)
							{
								const tr = generateElement("tr");
								const td = generateElement("td");
								if(i === 2)
								{
									td.innerText = key;
								}
								else
								{
									updateDispatch(td, data[key]);
								}
								tr.appendChild(td);
							}
							fragment.appendChild(tr);
						}
					}
					element.appendChild(fragment);
				}
				else
				{
					defaultFunc();
				}
			};

			const functionMapping = {
				ol: lists,
				ul: lists,
				table: tables,
				tbody: tables,
				thead: tables,
				tfoot: tables
			};

			( functionMapping[element.nodeName.toLowerCase()] || defaultFunc )();
			element.ShurikenCleared = false;
		}

		function isTagOutBindable(elementTag)
		{
			const elements = {
				input: true, textarea: true
			};
			return elements[elementTag.toLowerCase()];
		}

		function delayUntilWindowLoad(callback)
		{
			if(!windowLoaded)
			{
				window.onload = callback;
			}
			else
			{
				callback();
			}
		}

		function isHTMLElement(element)
		{
			let current = element.__proto__;
			while(current !== null)
			{
				if(current.toString() === "[object HTMLElement]")
				{
					return true;
				}
				current = current.__proto__;
			}
			return false;
		}

		//END INTERNAL FUNCTIONS

		//API FUNCTIONS

		function API_eventListener(elementOrSelector, event, callback)
		{
			if(isHTMLElement(elementOrSelector))
			{
				elementOrSelector[event] = callback;
			}
			else
			{
				delayUntilWindowLoad(function(){ document.querySelector(elementOrSelector)[event] = callback; });
			}
		}

		function API_init(name, value)
		{
			if(!keysInitialized[name])
			{
				keysInitialized[name]= true;
				API_set(name, value);
				Object.defineProperty(API.Data, name,
				{
					set: function(value)
					{
						API_set(name, value);
					},
					get: function()
					{
						return API_get(name);
					}
				});
			}
		}

		function API_set(name, value)
		{
			dataStorage.set(name, value);
			refreshName(name);
		}

		function API_get(name)
		{
			return dataStorage.get(name);
		}

		function API_bind(elementOrSelector, name)
		{
			const DOMelement = elementOrSelector;
			let DOMElementDirectRef = DOMelement;
			//if(typeof DOMelement === "string")
			if(!isHTMLElement(DOMelement))
			{
				delayUntilWindowLoad(function()
				{
					DOMElementDirectRef = document.querySelector(DOMelement);
				});
			}
			if(name !== undefined)
			{
				delayUntilWindowLoad(function()
				{
					if(isTagOutBindable(DOMElementDirectRef.nodeName))
					{
						API_bind(DOMelement).out(name);
					}
				});

				return API_bind(DOMelement).in(name);
			}
			else
			{
				return{
					in: function(name)
					{
						delayUntilWindowLoad(function()
						{
							( nameBindings[name] || (nameBindings[name] = []) ).push(DOMElementDirectRef);
							DOMElementDirectRef.ShurikenNameBind = name;
							refreshElement(DOMElementDirectRef);
						});
					},
					out: function(name)
					{
						delayUntilWindowLoad(function()
						{
							if(isTagOutBindable(DOMElementDirectRef.nodeName))
							{
								DOMElementDirectRef.oninput = function()
								{
									API_set(name, DOMElementDirectRef.value);
								}
								API_set(name, DOMElementDirectRef.value);
							}
							else
							{
								console.warn(`${frameworkName}: out binding is not available for ${DOMelement} because it doesn't take user input.`);
							}
						});
						return API_bind;
					}
				}
			}
		}

		//
		Object.assign(API.Data, {
			init: API_init,
			set: API_set,
			get: API_get,
			bind: API_bind,
			eventListener: API_eventListener
		});
		//Object.freeze(API.Data);
		return Object.freeze(API);
	
	})();
})();