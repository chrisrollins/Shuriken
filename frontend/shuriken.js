const Shuriken = (function()
{
	if(!localStorage.Shuriken)
	{
		localStorage.Shuriken = "{}";
	}
	const data = JSON.parse(localStorage.Shuriken);
	const nameBindings = {};
	let ShurikenElementCount = 0;

	const generateElement = function(elementType)
	{
		const el = document.createElement(elementType);
		el.ShurikenID = ShurikenElementCount++;
		el.ShurikenCleared = true;
		return el;
	};

	const clearElement = function(element)
	{
		if(!element.ShurikenCleared)
		{
			const children = Array.prototype.slice.call(element.childNodes);
			for(const child of children)
			{
				if(child.ShurikenID !== undefined)
				{
					child.remove();
				}
			}
			element.ShurikenCleared = true;
		}
	};

	const refreshElement = function(element)
	{
		updateDispatch(element, data[element.ShurikenNameBind]);
	};

	const refreshName = function(name)
	{
		for(const element of (nameBindings[name] || []))
		{
			updateDispatch(element, data[name]);
		}
	};

	const updateDispatch = function(element, data)
	{
		clearElement(element);
		const defaultFunc = function()
		{
			if(Array.isArray(data))
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

		};

		const lists = function()
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
		};

		const tables = function()
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
	};

	const isTagOutBindable = function(elementTag)
	{
		const elements = {
			input: true, textarea: true
		};
		return elements[elementTag.toLowerCase()];
	}


	return Object.freeze({
		Data: {
			set: function(name, value)
			{
				data[name] = value;
				localStorage.Shuriken = JSON.stringify(data);
				refreshName(name);
			},
			get: function(name)
			{
				return data[name];
			},
			bind: function(DOMelement, name)
			{
				if(typeof DOMelement === "string")
				{
					DOMelement = document.querySelector(DOMelement);
				}
				if(name !== undefined)
				{
					if(isTagOutBindable(DOMelement.nodeName))
					{
						Shuriken.Data.bind(DOMelement).out(name);
					}
					return Shuriken.Data.bind(DOMelement).in(name);
				}
				else
				{
					return{
						in: function(name)
						{
							( nameBindings[name] || (nameBindings[name] = []) ).push(DOMelement);
							DOMelement.ShurikenNameBind = name;
							refreshElement(DOMelement);
						},
						out: function(name)
						{
							if(isTagOutBindable(DOMelement.nodeName))
							{
								DOMelement.oninput = function()
								{
									Shuriken.Data.set(name, DOMelement.value);
								}
								//do this on initial bind too
								Shuriken.Data.set(name, DOMelement.value);
							}
							else
							{
								console.warn(`out binding is not available for ${DOMelement} because it doesn't take user input.`);
							}
							return Shuriken.Data.bind;
						}
					}
				}
			}
		},
		WebSockets: {}
	});

})();