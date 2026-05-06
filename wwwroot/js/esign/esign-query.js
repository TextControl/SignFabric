window.esignQuery = window.esignQuery || (function () {
    function toArray(value) {
        return Array.prototype.slice.call(value || []);
    }

    function normalizeEventName(eventName) {
        var parts = (eventName || "").split(".");
        return {
            type: parts[0],
            namespace: parts.slice(1).join(".")
        };
    }

    function wrap(elements) {
        return {
            addClass: function (classNames) {
                var names = (classNames || "").split(/\s+/).filter(Boolean);
                elements.forEach(function (element) { element.classList.add.apply(element.classList, names); });
                return this;
            },
            removeClass: function (classNames) {
                var names = (classNames || "").split(/\s+/).filter(Boolean);
                elements.forEach(function (element) { element.classList.remove.apply(element.classList, names); });
                return this;
            },
            empty: function () {
                elements.forEach(function (element) { element.innerHTML = ""; });
                return this;
            },
            append: function (content) {
                elements.forEach(function (element) {
                    if (typeof content === "string") {
                        element.insertAdjacentHTML("beforeend", content);
                    }
                    else if (content instanceof Node) {
                        element.appendChild(content);
                    }
                });
                return this;
            },
            attr: function (name, value) {
                if (value === undefined) {
                    return elements[0] ? elements[0].getAttribute(name) : undefined;
                }

                elements.forEach(function (element) { element.setAttribute(name, value); });
                return this;
            },
            removeAttr: function (name) {
                elements.forEach(function (element) { element.removeAttribute(name); });
                return this;
            },
            prop: function (name, value) {
                if (value === undefined) {
                    return elements[0] ? elements[0][name] : undefined;
                }

                elements.forEach(function (element) { element[name] = value; });
                return this;
            },
            val: function (value) {
                if (value === undefined) {
                    return elements[0] ? elements[0].value : undefined;
                }

                elements.forEach(function (element) { element.value = value; });
                return this;
            },
            text: function (value) {
                if (value === undefined) {
                    return elements[0] ? elements[0].textContent : undefined;
                }

                elements.forEach(function (element) { element.textContent = value; });
                return this;
            },
            html: function (value) {
                if (value === undefined) {
                    return elements[0] ? elements[0].innerHTML : undefined;
                }

                elements.forEach(function (element) { element.innerHTML = value; });
                return this;
            },
            hide: function () {
                elements.forEach(function (element) { element.style.display = "none"; });
                return this;
            },
            show: function () {
                elements.forEach(function (element) { element.style.display = ""; });
                return this;
            },
            css: function (name, value) {
                elements.forEach(function (element) { element.style.setProperty(name, value); });
                return this;
            },
            parent: function () {
                return wrap(elements.map(function (element) { return element.parentElement; }).filter(Boolean));
            },
            each: function (callback) {
                elements.forEach(function (element, index) { callback.call(element, index, element); });
                return this;
            },
            on: function (eventName, handler) {
                var parsed = normalizeEventName(eventName);
                elements.forEach(function (element) {
                    element.__esignQueryEvents = element.__esignQueryEvents || [];
                    var wrapped = function (event) { handler.call(element, event); };
                    element.__esignQueryEvents.push({
                        type: parsed.type,
                        namespace: parsed.namespace,
                        wrapped: wrapped
                    });
                    element.addEventListener(parsed.type, wrapped);
                });
                return this;
            },
            off: function (eventName) {
                var parsed = normalizeEventName(eventName);
                elements.forEach(function (element) {
                    var events = element.__esignQueryEvents || [];
                    element.__esignQueryEvents = events.filter(function (entry) {
                        var matchesType = !parsed.type || entry.type === parsed.type;
                        var matchesNamespace = !parsed.namespace || entry.namespace === parsed.namespace;
                        var remove = matchesType && matchesNamespace;
                        if (remove) {
                            element.removeEventListener(entry.type, entry.wrapped);
                        }
                        return !remove;
                    });
                });
                return this;
            }
        };
    }

    return function (selector) {
        if (selector instanceof Node) {
            return wrap([selector]);
        }

        if (selector instanceof NodeList || Array.isArray(selector)) {
            return wrap(toArray(selector));
        }

        return wrap(toArray(document.querySelectorAll(selector)));
    };
}());

window.$ = window.$ || window.esignQuery;
