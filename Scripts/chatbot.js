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

    function addMessage(text, isUser) {
        var message = document.createElement('p');
        message.className = 'news-chat__message ' + (isUser ? 'news-chat__message--user' : 'news-chat__message--bot');
        message.textContent = text;
        messages.appendChild(message);
        messages.scrollTop = messages.scrollHeight;
    }

    function addSources(sources) {
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

        if (list.children.length > 0) {
            messages.appendChild(list);
            messages.scrollTop = messages.scrollHeight;
        }
    }

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
                addMessage(data.answer || 'Chatbot chưa tạo được câu trả lời.', false);
                addSources(data.sources);
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
