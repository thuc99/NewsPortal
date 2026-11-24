(function () {
    var widget = document.querySelector('.news-chat');
    if (!widget) return;

    var toggle = widget.querySelector('.news-chat__toggle');
    var panel = widget.querySelector('.news-chat__panel');
    var closeButton = widget.querySelector('.news-chat__close');
    var form = widget.querySelector('.news-chat__form');
    var input = form.querySelector('input[name="message"]');
    var token = form.querySelector('input[name="__RequestVerificationToken"]');
    var submitButton = form.querySelector('button[type="submit"]');
    var messages = widget.querySelector('.news-chat__messages');
    var status = widget.querySelector('.news-chat__status');
    var historyKey = 'tinNhanhChatHistory';
    var historyExpiryKey = 'tinNhanhChatHistoryExpiry';
    var historyTtl = 5 * 60 * 1000;
    var chatHistory = loadHistory();

    function clearHistory() {
        try {
            window.localStorage.removeItem(historyKey);
            window.localStorage.removeItem(historyExpiryKey);
            window.sessionStorage.removeItem(historyKey);
        } catch (error) {
            return;
        }
    }

    function loadHistory() {
        try {
            var expiresAt = Number(window.localStorage.getItem(historyExpiryKey));
            if (expiresAt && Date.now() >= expiresAt) {
                clearHistory();
                return [];
            }

            var stored = window.localStorage.getItem(historyKey);
            if (!stored) {
                stored = window.sessionStorage.getItem(historyKey) || '[]';
                window.localStorage.setItem(historyKey, stored);
            }
            var history = JSON.parse(stored);
            if (Array.isArray(history) && history.length > 0 && !expiresAt) {
                window.localStorage.setItem(historyExpiryKey, String(Date.now() + historyTtl));
            }
            return Array.isArray(history) ? history.slice(-60) : [];
        } catch (error) {
            return [];
        }
    }

    function saveHistory() {
        try {
            window.localStorage.setItem(historyKey, JSON.stringify(chatHistory.slice(-60)));
            window.localStorage.setItem(historyExpiryKey, String(Date.now() + historyTtl));
        } catch (error) {
            return;
        }
    }

    function scheduleHistoryCleanup() {
        try {
            if (chatHistory.length > 0) {
                window.localStorage.setItem(historyExpiryKey, String(Date.now() + historyTtl));
            }
        } catch (error) {
            return;
        }
    }

    window.addEventListener('pagehide', scheduleHistoryCleanup);
    window.addEventListener('pageshow', function () {
        var expiresAt;
        try {
            expiresAt = Number(window.localStorage.getItem(historyExpiryKey));
        } catch (error) {
            return;
        }

        if (expiresAt && Date.now() >= expiresAt) {
            chatHistory = [];
            clearHistory();
            messages.textContent = '';
            addMessage('Xin chào! Mình là trợ lí AI Tin Nhanh. Bạn có thể hỏi về tin tức, nhờ tóm tắt bài viết hoặc hỏi thời tiết hôm nay.', false, null, false);
        }
    });

    function renderSources(sources) {
        if (!Array.isArray(sources) || sources.length === 0) return;

        var list = document.createElement('ul');
        list.className = 'news-chat__sources';

        sources.forEach(function (source) {
            if (!source || typeof source.title !== 'string' || typeof source.url !== 'string') return;

            var url;
            try {
                url = new URL(source.url, window.location.href);
            } catch (error) {
                return;
            }

            if (url.origin !== window.location.origin || (url.protocol !== 'https:' && url.protocol !== 'http:')) return;

            var item = document.createElement('li');
            var link = document.createElement('a');
            link.href = url.href;
            link.textContent = source.title;
            item.appendChild(link);
            list.appendChild(item);
        });

        if (list.children.length > 0) messages.appendChild(list);
    }

    function addMessage(text, isUser, sources, persist) {
        var message = document.createElement('p');
        message.className = 'news-chat__message ' + (isUser ? 'news-chat__message--user' : 'news-chat__message--bot');
        message.textContent = text;
        messages.appendChild(message);

        renderSources(sources);
        messages.scrollTop = messages.scrollHeight;

        if (persist !== false) {
            chatHistory.push({
                text: String(text).substring(0, 2000),
                isUser: !!isUser,
                sources: Array.isArray(sources) ? sources.slice(0, 10) : []
            });
            chatHistory = chatHistory.slice(-60);
            saveHistory();
        }
    }

    chatHistory.forEach(function (entry) {
        if (entry && typeof entry.text === 'string') {
            addMessage(entry.text, !!entry.isUser, entry.sources, false);
        }
    });

    function setPanelOpen(isOpen) {
        panel.hidden = !isOpen;
        document.body.classList.toggle('news-chat-open', isOpen);
        toggle.setAttribute('aria-expanded', isOpen ? 'true' : 'false');
    }

    function getWeatherContext(message) {
        var weatherTerms = [
            'thời tiết', 'nhiệt độ', 'độ ẩm', 'dự báo thời tiết', 'trời hôm nay',
            'mưa không', 'có mưa không', 'mưa hôm nay', 'nắng không'
        ];
        if (!weatherTerms.some(function (term) { return message.toLowerCase().indexOf(term) >= 0; })) return '';

        var status = document.getElementById('weather-status');
        var temperature = document.getElementById('weather-temp-summary');
        var city = document.getElementById('weather-city-summary');
        var date = document.getElementById('weather-date-full');
        var condition = document.getElementById('weather-condition');
        var range = document.getElementById('weather-range');
        var humidity = document.getElementById('weather-humidity');
        if (!status || status.textContent.trim() !== 'Dữ liệu thời tiết trực tiếp từ Open-Meteo.' ||
            !temperature || temperature.textContent.indexOf('--') >= 0) return '';

        return [
            'Nguồn: tiện ích thời tiết NewsPortal (Open-Meteo).',
            'Thành phố: ' + (city ? city.textContent.trim() : ''),
            'Ngày: ' + (date ? date.textContent.trim() : ''),
            'Nhiệt độ hiện tại: ' + temperature.textContent.trim(),
            'Điều kiện: ' + (condition ? condition.textContent.trim() : ''),
            'Khoảng nhiệt độ trong ngày: ' + (range ? range.textContent.trim() : ''),
            'Độ ẩm: ' + (humidity ? humidity.textContent.trim() : '')
        ].join('\n');
    }

    toggle.addEventListener('click', function () {
        setPanelOpen(true);
        input.focus();
    });

    closeButton.addEventListener('click', function () {
        setPanelOpen(false);
        toggle.focus();
    });

    document.addEventListener('keydown', function (event) {
        if (event.key === 'Escape' && !panel.hidden) {
            setPanelOpen(false);
            toggle.focus();
        }
    });

    form.addEventListener('submit', function (event) {
        event.preventDefault();
        var message = input.value.trim();
        if (!message || message.length > 500 || !token.value) return;

        addMessage(message, true);
        input.value = '';
        input.disabled = true;
        submitButton.disabled = true;
        status.textContent = 'Đang trả lời...';

        var weatherContext = getWeatherContext(message);
        var body = '__RequestVerificationToken=' + encodeURIComponent(token.value) +
            '&message=' + encodeURIComponent(message) +
            '&weatherContext=' + encodeURIComponent(weatherContext);

        fetch(widget.getAttribute('data-chat-url'), {
            method: 'POST',
            credentials: 'same-origin',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8',
                'X-Requested-With': 'XMLHttpRequest'
            },
            body: body
        })
            .then(function (response) {
                return response.json().then(function (data) {
                    if (!response.ok) throw new Error(data.error || 'Không thể gửi câu hỏi lúc này.');
                    return data;
                });
            })
            .then(function (data) {
                addMessage(data.answer || 'Chatbot chưa tạo được câu trả lời.', false, data.sources);
                status.textContent = '';
            })
            .catch(function (error) {
                addMessage(error.message || 'Không thể kết nối chatbot. Vui lòng thử lại.', false);
                status.textContent = '';
            })
            .then(function () {
                input.disabled = false;
                submitButton.disabled = false;
                input.focus();
            });
    });
}());
