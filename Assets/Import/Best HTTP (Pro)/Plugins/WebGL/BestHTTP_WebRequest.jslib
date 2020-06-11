var Lib_BEST_HTTP_WebGL_HTTP_Bridge =
{
	/*LogLevels: {
		All: 0,
		Information: 1,
		Warning: 2,
		Error: 3,
		Exception: 4,
		None: 5
	}*/

	$wr: {
		requestInstances: {},
		nextRequestId: 1,
		loglevel: 2
	},

	XHR_Create: function(method, url, user, passwd, withCredentials)
	{
		var _url = /*encodeURI*/(Pointer_stringify(url))
					.replace(/\+/g, '%2B')
					.replace(/%252[fF]/ig, '%2F');
		var _method = Pointer_stringify(method);

		if (wr.loglevel <= 1) /*information*/
			console.log(wr.nextRequestId + ' XHR_Create - withCredentials: ' + withCredentials + ' method: ' + _method + ' url: ' + _url);

		var http = new XMLHttpRequest();

		if (user && passwd)
		{
			var u = Pointer_stringify(user);
			var p = Pointer_stringify(passwd);

			http.withCredentials = true;
			http.open(_method, _url, /*async:*/ true , u, p);
		}
		else {
            http.withCredentials = withCredentials;
			http.open(_method, _url, /*async:*/ true);
        }

		http.responseType = 'arraybuffer';

		wr.requestInstances[wr.nextRequestId] = http;
		return wr.nextRequestId++;
	},

	XHR_SetTimeout: function (request, timeout)
	{
		if (wr.loglevel <= 1) /*information*/
			console.log(request + ' XHR_SetTimeout ' + timeout);

		wr.requestInstances[request].timeout = timeout;
	},

	XHR_SetRequestHeader: function (request, header, value)
	{
		var _header = Pointer_stringify(header);
		var _value = Pointer_stringify(value);

		if (wr.loglevel <= 1) /*information*/
			console.log(request + ' XHR_SetRequestHeader ' + _header + ' ' + _value);

        if (_header != 'Cookie')
		    wr.requestInstances[request].setRequestHeader(_header, _value);
        else {
            var cookies = _value.split(';');
            for (var i = 0; i < cookies.length; i++) {
                document.cookie = cookies[i];
            }
        }
	},

	XHR_SetResponseHandler: function (request, onresponse, onerror, ontimeout, onaborted)
	{
		if (wr.loglevel <= 1) /*information*/
			console.log(request + ' XHR_SetResponseHandler');

		var http = wr.requestInstances[request];
		// LOAD
		http.onload = function http_onload(e) {
			if (wr.loglevel <= 1) /*information*/
				console.log(request + '  - onload ' + http.status + ' ' + http.statusText);

			if (onresponse)
			{
				var response = 0;
				if (!!http.response)
					response = http.response;

				var byteArray = new Uint8Array(response);
				var buffer = _malloc(byteArray.length);
				HEAPU8.set(byteArray, buffer);

				Runtime.dynCall('viiiii', onresponse, [request, http.status, buffer, byteArray.length, 0]);

				_free(buffer);
			}
		};

		if (onerror)
		{
			http.onerror = function http_onerror(e) {
				function HandleError(err)
				{
					var length = lengthBytesUTF8(err) + 1;
					var buffer = _malloc(length);

					stringToUTF8Array(err, HEAPU8, buffer, length);

					Runtime.dynCall('vii', onerror, [request, buffer]);

					_free(buffer);
				}

				if (e.error)
					HandleError(e.error);
				else
					HandleError("Unknown Error! Maybe a CORS porblem?");
			};
		}

		if (ontimeout)
			http.ontimeout = function http_onerror(e) {
				Runtime.dynCall('vi', ontimeout, [request]);
			};

		if (onaborted)
			http.onabort = function http_onerror(e) {
				Runtime.dynCall('vi', onaborted, [request]);
			};
	},

	XHR_SetProgressHandler: function (request, onprogress, onuploadprogress)
	{
		if (wr.loglevel <= 1) /*information*/
			console.log(request + ' XHR_SetProgressHandler');

		var http = wr.requestInstances[request];
		if (http)
		{
			if (onprogress)
				http.onprogress = function http_onprogress(e) {
					if (wr.loglevel <= 1) /*information*/
						console.log(request + ' XHR_SetProgressHandler - onProgress ' + e.loaded + ' ' + e.total);

					if (e.lengthComputable)
						Runtime.dynCall('viii', onprogress, [request, e.loaded, e.total]);
				};

			if (onuploadprogress)
				http.upload.addEventListener("progress", function http_onprogress(e) {
					if (wr.loglevel <= 1) /*information*/
						console.log(request + ' XHR_SetProgressHandler - onUploadProgress ' + e.loaded + ' ' + e.total);

					if (e.lengthComputable)
						Runtime.dynCall('viii', onuploadprogress, [request, e.loaded, e.total]);
				}, true);
		}
	},

	XHR_Send: function (request, ptr, length)
	{
		if (wr.loglevel <= 1) /*information*/
			console.log(request + ' XHR_Send ' + ptr + ' ' + length);

		var http = wr.requestInstances[request];

		try {
			if (length > 0)
				http.send(HEAPU8.subarray(ptr, ptr+length));
			else
				http.send();
		}
		catch(e) {
			if (wr.loglevel <= 4) /*exception*/
				console.error(request + ' ' + e.name + ": " + e.message);
		}
	},

	XHR_GetResponseHeaders: function(request, callback)
	{
		if (wr.loglevel <= 1) /*information*/
			console.log(request + ' XHR_GetResponseHeaders');

        var headers = ''
        var cookies = document.cookie.split(';');
        for(var i = 0; i < cookies.length; ++i)
            headers += "Set-Cookie:" + cookies[i] + "\r\n";

        var additionalHeaders = wr.requestInstances[request].getAllResponseHeaders().trim();
        if (additionalHeaders.length > 0) {
            headers += additionalHeaders;
            headers += "\r\n";
        }

		if (wr.loglevel <= 1) /*information*/
			console.log('  "' + headers + '"');

		var byteArray = new Uint8Array(headers.length);
		for(var i=0,j=headers.length;i<j;++i){
			byteArray[i]=headers.charCodeAt(i);
		}

		var buffer = _malloc(byteArray.length);
		HEAPU8.set(byteArray, buffer);

		Runtime.dynCall('viii', callback, [request, buffer, byteArray.length]);

		_free(buffer);
	},

	XHR_GetStatusLine: function(request, callback)
	{
		if (wr.loglevel <= 1) /*information*/
			console.log(request + ' XHR_GetStatusLine');

		var status = "HTTP/1.1 " + wr.requestInstances[request].status + " " + wr.requestInstances[request].statusText;

		if (wr.loglevel <= 1) /*information*/
			console.log(status);

		var byteArray = new Uint8Array(status.length);
		for(var i=0,j=status.length;i<j;++i){
			byteArray[i]=status.charCodeAt(i);
		}
		var buffer = _malloc(byteArray.length);
		HEAPU8.set(byteArray, buffer);

		Runtime.dynCall('viii', callback, [request, buffer, byteArray.length]);

		_free(buffer);
	},

	XHR_Abort: function (request)
	{
		if (wr.loglevel <= 1) /*information*/
			console.log(request + ' XHR_Abort');

		wr.requestInstances[request].abort();
	},

	XHR_Release: function (request)
	{
		if (wr.loglevel <= 1) /*information*/
			console.log(request + ' XHR_Release');

		delete wr.requestInstances[request];
	},

	XHR_SetLoglevel: function (level)
	{
		wr.loglevel = level;
	}
};

autoAddDeps(Lib_BEST_HTTP_WebGL_HTTP_Bridge, '$wr');
mergeInto(LibraryManager.library, Lib_BEST_HTTP_WebGL_HTTP_Bridge);
