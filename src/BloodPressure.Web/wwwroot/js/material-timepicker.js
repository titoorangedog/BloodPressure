(function () {
    "use strict";

    function createElement(tag, className, text) {
        var element = document.createElement(tag);
        if (className) {
            element.className = className;
        }
        if (typeof text === "string") {
            element.textContent = text;
        }
        return element;
    }

    function pad2(value) {
        return String(value).padStart(2, "0");
    }

    function clamp(value, min, max) {
        return Math.min(max, Math.max(min, value));
    }

    function parseTime(value) {
        var match = /^([01]\d|2[0-3]):([0-5]\d)$/.exec((value || "").trim());
        if (match) {
            return {
                hour24: Number(match[1]),
                minute: Number(match[2])
            };
        }

        var now = new Date();
        return {
            hour24: now.getHours(),
            minute: now.getMinutes()
        };
    }

    function to12Hour(hour24) {
        var meridiem = hour24 >= 12 ? "PM" : "AM";
        var hour12 = hour24 % 12;
        if (hour12 === 0) {
            hour12 = 12;
        }

        return { hour12: hour12, meridiem: meridiem };
    }

    function to24Hour(hour12, meridiem) {
        if (meridiem === "AM") {
            return hour12 % 12;
        }

        return (hour12 % 12) + 12;
    }

    function materialTimePicker() {
        var state = {
            isOpen: false,
            mode: "dial",
            selecting: "hour",
            hour24: 0,
            minute: 0,
            meridiem: "AM",
            targetInput: null
        };

        var ui = null;

        function ensureUi() {
            if (ui) {
                return ui;
            }

            var overlay = createElement("div", "bp-timepicker-overlay");
            var dialog = createElement("div", "bp-timepicker-dialog");
            dialog.setAttribute("role", "dialog");
            dialog.setAttribute("aria-modal", "true");
            overlay.appendChild(dialog);

            var title = createElement("div", "bp-timepicker-title", "Select time");
            dialog.appendChild(title);

            var header = createElement("div", "bp-timepicker-header");
            var hourChip = createElement("button", "bp-timepicker-chip bp-timepicker-hour-chip", "12");
            var separator = createElement("span", "bp-timepicker-separator", ":");
            var minuteChip = createElement("button", "bp-timepicker-chip bp-timepicker-minute-chip", "00");
            var meridiem = createElement("div", "bp-timepicker-meridiem");
            var amButton = createElement("button", "bp-timepicker-meridiem-btn", "AM");
            var pmButton = createElement("button", "bp-timepicker-meridiem-btn", "PM");
            meridiem.appendChild(amButton);
            meridiem.appendChild(pmButton);
            header.appendChild(hourChip);
            header.appendChild(separator);
            header.appendChild(minuteChip);
            header.appendChild(meridiem);
            dialog.appendChild(header);

            var dialContainer = createElement("div", "bp-timepicker-dial-container");
            var dial = createElement("div", "bp-timepicker-dial");
            var hand = createElement("div", "bp-timepicker-hand");
            var center = createElement("div", "bp-timepicker-center");
            dial.appendChild(hand);
            dial.appendChild(center);
            dialContainer.appendChild(dial);
            dialog.appendChild(dialContainer);

            var inputContainer = createElement("div", "bp-timepicker-input-container");
            var inputGrid = createElement("div", "bp-timepicker-input-grid");
            var hourInputWrap = createElement("div", "bp-timepicker-input-wrap");
            var hourInput = createElement("input", "bp-timepicker-number");
            hourInput.type = "number";
            hourInput.min = "1";
            hourInput.max = "12";
            hourInputWrap.appendChild(hourInput);
            hourInputWrap.appendChild(createElement("div", "bp-timepicker-input-label", "Hour"));

            var minuteInputWrap = createElement("div", "bp-timepicker-input-wrap");
            var minuteInput = createElement("input", "bp-timepicker-number");
            minuteInput.type = "number";
            minuteInput.min = "0";
            minuteInput.max = "59";
            minuteInputWrap.appendChild(minuteInput);
            minuteInputWrap.appendChild(createElement("div", "bp-timepicker-input-label", "Minute"));

            var inputSeparator = createElement("span", "bp-timepicker-separator bp-timepicker-separator-input", ":");
            inputGrid.appendChild(hourInputWrap);
            inputGrid.appendChild(inputSeparator);
            inputGrid.appendChild(minuteInputWrap);
            inputGrid.appendChild(meridiem.cloneNode(true));
            inputContainer.appendChild(inputGrid);
            dialog.appendChild(inputContainer);

            var inputMeridiem = inputGrid.querySelector(".bp-timepicker-meridiem");
            var inputAmButton = inputMeridiem.children[0];
            var inputPmButton = inputMeridiem.children[1];

            var footer = createElement("div", "bp-timepicker-footer");
            var switchMode = createElement("button", "bp-timepicker-icon-btn", "\u2318");
            switchMode.setAttribute("title", "Toggle input mode");
            var actions = createElement("div", "bp-timepicker-actions");
            var cancelButton = createElement("button", "bp-timepicker-action", "Cancel");
            var okButton = createElement("button", "bp-timepicker-action", "OK");
            actions.appendChild(cancelButton);
            actions.appendChild(okButton);
            footer.appendChild(switchMode);
            footer.appendChild(actions);
            dialog.appendChild(footer);

            overlay.addEventListener("click", function (event) {
                if (event.target === overlay) {
                    close(false);
                }
            });

            document.addEventListener("keydown", function (event) {
                if (!state.isOpen) {
                    return;
                }

                if (event.key === "Escape") {
                    event.preventDefault();
                    close(false);
                }
            });

            hourChip.addEventListener("click", function () {
                state.selecting = "hour";
                refresh();
            });

            minuteChip.addEventListener("click", function () {
                state.selecting = "minute";
                refresh();
            });

            amButton.addEventListener("click", function () {
                setMeridiem("AM");
            });

            pmButton.addEventListener("click", function () {
                setMeridiem("PM");
            });

            inputAmButton.addEventListener("click", function () {
                setMeridiem("AM");
            });

            inputPmButton.addEventListener("click", function () {
                setMeridiem("PM");
            });

            hourInput.addEventListener("input", function () {
                var next = clamp(Number(hourInput.value || "0"), 1, 12);
                if (Number.isFinite(next) && next > 0) {
                    state.hour24 = to24Hour(next, state.meridiem);
                    refresh();
                }
            });

            minuteInput.addEventListener("input", function () {
                var next = clamp(Number(minuteInput.value || "0"), 0, 59);
                if (Number.isFinite(next)) {
                    state.minute = next;
                    refresh();
                }
            });

            switchMode.addEventListener("click", function () {
                state.mode = state.mode === "dial" ? "input" : "dial";
                refresh();
            });

            cancelButton.addEventListener("click", function () {
                close(false);
            });

            okButton.addEventListener("click", function () {
                close(true);
            });

            document.body.appendChild(overlay);

            ui = {
                overlay: overlay,
                title: title,
                hourChip: hourChip,
                minuteChip: minuteChip,
                amButton: amButton,
                pmButton: pmButton,
                dialContainer: dialContainer,
                dial: dial,
                hand: hand,
                inputContainer: inputContainer,
                hourInput: hourInput,
                minuteInput: minuteInput,
                inputAmButton: inputAmButton,
                inputPmButton: inputPmButton,
                switchMode: switchMode
            };

            return ui;
        }

        function setMeridiem(next) {
            var current12 = to12Hour(state.hour24).hour12;
            state.meridiem = next;
            state.hour24 = to24Hour(current12, next);
            refresh();
        }

        function refreshDial() {
            var current = ensureUi();
            var dial = current.dial;

            Array.from(dial.querySelectorAll(".bp-timepicker-number")).forEach(function (node) {
                node.remove();
            });

            var total = state.selecting === "hour" ? 12 : 12;
            var radius = 108;
            var center = 124;
            var selectedValue = state.selecting === "hour"
                ? to12Hour(state.hour24).hour12
                : Math.floor(state.minute / 5) * 5;

            for (var i = 0; i < total; i += 1) {
                var value = state.selecting === "hour" ? (i + 1) : (i * 5);
                var label = state.selecting === "hour" ? String(value) : pad2(value);
                var angle = (i * 30) - 60;
                var radians = angle * (Math.PI / 180);
                var x = center + (Math.cos(radians) * radius);
                var y = center + (Math.sin(radians) * radius);

                var number = createElement("button", "bp-timepicker-number", label);
                number.style.left = x + "px";
                number.style.top = y + "px";
                if (value === selectedValue) {
                    number.classList.add("active");
                }

                (function (picked) {
                    number.addEventListener("click", function () {
                        if (state.selecting === "hour") {
                            state.hour24 = to24Hour(picked, state.meridiem);
                            state.selecting = "minute";
                        } else {
                            state.minute = picked;
                        }

                        refresh();
                    });
                })(value);

                dial.appendChild(number);
            }

            var angleValue = state.selecting === "hour"
                ? (to12Hour(state.hour24).hour12 * 30)
                : (state.minute * 6);

            current.hand.style.transform = "translateX(-50%) rotate(" + angleValue + "deg)";
            current.hand.style.height = (state.selecting === "hour" ? 78 : 98) + "px";
        }

        function refresh() {
            var current = ensureUi();
            var converted = to12Hour(state.hour24);
            var hour12 = converted.hour12;

            current.hourChip.textContent = pad2(hour12);
            current.minuteChip.textContent = pad2(state.minute);
            current.hourChip.classList.toggle("active", state.selecting === "hour");
            current.minuteChip.classList.toggle("active", state.selecting === "minute");
            current.amButton.classList.toggle("active", state.meridiem === "AM");
            current.pmButton.classList.toggle("active", state.meridiem === "PM");
            current.inputAmButton.classList.toggle("active", state.meridiem === "AM");
            current.inputPmButton.classList.toggle("active", state.meridiem === "PM");
            current.hourInput.value = pad2(hour12);
            current.minuteInput.value = pad2(state.minute);

            var isDial = state.mode === "dial";
            current.title.textContent = isDial ? "Select time" : "Enter time";
            current.switchMode.textContent = isDial ? "\u2328" : "\uD83D\uDD52";
            current.dialContainer.style.display = isDial ? "block" : "none";
            current.inputContainer.style.display = isDial ? "none" : "block";

            if (isDial) {
                refreshDial();
            }
        }

        function open(targetInput) {
            var current = ensureUi();
            var parsed = parseTime(targetInput.value);
            state.hour24 = parsed.hour24;
            state.minute = parsed.minute;
            state.meridiem = parsed.hour24 >= 12 ? "PM" : "AM";
            state.selecting = "hour";
            state.mode = "dial";
            state.targetInput = targetInput;
            state.isOpen = true;

            refresh();
            current.overlay.classList.add("open");
            document.body.classList.add("modal-open");
        }

        function close(apply) {
            var current = ensureUi();
            current.overlay.classList.remove("open");
            document.body.classList.remove("modal-open");
            state.isOpen = false;

            if (apply && state.targetInput) {
                var value = pad2(state.hour24) + ":" + pad2(state.minute);
                state.targetInput.value = value;
                state.targetInput.dispatchEvent(new Event("input", { bubbles: true }));
                state.targetInput.dispatchEvent(new Event("change", { bubbles: true }));
            }
        }

        function attach(elements) {
            if (!elements) {
                return;
            }

            Array.from(elements).forEach(function (element) {
                if (!element || element.dataset.bpMaterialTimeAttached === "1") {
                    return;
                }

                element.dataset.bpMaterialTimeAttached = "1";
                element.type = "text";
                element.readOnly = true;
                element.placeholder = "HH:mm";

                var openHandler = function () {
                    open(element);
                };

                element.addEventListener("click", openHandler);
                element.addEventListener("focus", openHandler);
            });
        }

        return {
            attach: attach
        };
    }

    window.bpMaterialTimePicker = materialTimePicker();
})();
