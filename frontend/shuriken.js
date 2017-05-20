const Shuriken = (function()
{
	const data = Object.assign({}, localStorage.Shuriken);
	const nameBindings = {};
	localStorage.Shuriken = {};

	const refreshElement = function(DOMelement)
	{
		updateDispatch(DOMelement, data[DOMelement.ShurikenNameBind]);
	};

	const refreshName = function(name)
	{
		for(let element of nameBindings[name] || [])
		{
			updateDispatch(element, data[name]);
		}
	};

	const updateDispatch = function(element, data)
	{
		const defaultFunc = function()
		{
			if(element.value)
			{
				element.value = data;
			}
			else
			{
				element.innerText = data;
			}
		};

		const lists = function()
		{
			if(Array.isArray(data))
			{
				let updateChunk = "";
				for(let item of data)
				{
					updateChunk += `<li>${item}</li>`
				}

				element.innerHTML = updateChunk;
			}
			else
			{
				defaultFunc();
			}
		};

		const functionMapping = {
			ol: lists,
			ul: lists
		};

		( functionMapping[element.nodeName.toLowerCase()] || defaultFunc )();
	}

	return Object.freeze({
		Document: {
			set: function(name, value)
			{
				data[name] = value;
				localStorage.Shuriken[name] = value;
				refreshName(name);
			},
			get: function(name)
			{
				return data[name];
			},
			bind: function(DOMelement, name)
			{
				(nameBindings[name] || (nameBindings[name] = [])).push(DOMelement);
				DOMelement.ShurikenNameBind = name;
				refreshElement(DOMelement);
			}
		},
		WebSockets: {}
	});
})();