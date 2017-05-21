const Shuriken = (function()
{
	const data = JSON.parse(localStorage.Shuriken);
	const nameBindings = {};
	let ShurikenElementCount = 0;

	const generateElement = function(elementType)
	{
		const el = document.createElement(elementType);
		el.ShurikenID = ShurikenElementCount++;
		return el;
	};

	const clearElement = function(element)
	{
		if(!element.ShurikenCleared)
		{
			const children = element.childNodes();
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
				const list = generateElement("ul");
				updateDispatch(list, data);
				element.appendChild(list);
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
				const fragment = document.createFragment();
				for(const item of data)
				{
					const li = document.generateElement("li");
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
			if(typeof data === "object" && data)
			{
				for(const key in data)
				{
					
				}
			}
		};

		const functionMapping = {
			ol: lists,
			ul: lists,
			table: tables
		};

		( functionMapping[element.nodeName.toLowerCase()] || defaultFunc )();
		element.ShurikenCleared = false;
	}

	return Object.freeze({
		Document: {
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
				( nameBindings[name] || (nameBindings[name] = []) ).push(DOMelement);
				DOMelement.ShurikenNameBind = name;
				refreshElement(DOMelement);
			}
		},
		WebSockets: {}
	});
})();