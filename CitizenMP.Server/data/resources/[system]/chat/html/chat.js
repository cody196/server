function colorize(string)
{
    var newString = '';
    var inSpan = false;

    for (i = 0; i < string.length; i++)
    {
        if (string[i] == '^')
        {
            if (string[i + 1] == '7' || string[i + 1] == '0')
            {
                if (inSpan)
                {
                    newString += '</span>';

                    inSpan = false;
                }

                i += 2;
            }
            else if (string[i + 1] >= '0' && string[i + 1] <= '9')
            {
                if (inSpan)
                {
                    newString += '</span>';
                }

                i += 2;
                newString += '<span class="color-' + string[i - 1] + '">';

                inSpan = true;
            }
        }

        newString += string[i];
    }

    if (inSpan)
    {
        newString += '</span>';
    }

    return newString;
}

$(function()
{
    var chatHideTimeout;

    function startHideChat()
    {
        if (chatHideTimeout)
        {
            clearTimeout(chatHideTimeout);
        }

        chatHideTimeout = setTimeout(function()
        {
            //$('#chat').hide(400);
        }, 5000);
    }

    $('#chatInput').fakeTextbox(); // //

    $('#chatInput')[0].onPress(function(e)
    {
        if (e.which == 13 || e.keyCode == 27)
        {
            $('#chatInputHas').hide();

            startHideChat();

            var obj = {};

            if (e.which == 13)
            {
                obj = { message: $(this).val() };
            }

            $(this).val('');

            $.post('http://chat/chatResult', JSON.stringify(obj), function(data)
            {
                console.log(data);
            });
        }
    });

    // on poll, request this
    //registerPollFunction(function()
    window.addEventListener('message', function(event)
    {
        if (event.data.type != 'poll')
        {
            return;
        }

        $.get('http://chat/getNew', function(data)
        {
            data.forEach(function(item)
            {
                if (item.meta && item.meta == 'openChatBox')
                {
                    $('#chat').show();

                    $('#chatInputHas').show();
                    $('#chatInput')[0].doFocus();

                    return;
                }

                // TODO: use some templating stuff for this
                var colorR = parseInt(item.color[0]);
                var colorG = parseInt(item.color[1]);
                var colorB = parseInt(item.color[2]);

                var name = item.name.replace('<', '&lt;');
                var message = item.message.replace('<', '&lt;');

                message = colorize(message);

                var buf = $('#chatBuffer');

                buf.find('ul').append('<li><strong style="color: rgb(' + colorR + ', ' + colorG + ', ' + colorB + ')">' + name + ': </strong>' + message + '</li>');
                buf.scrollTop(buf[0].scrollHeight - buf.height());

                $('#chat').show(0);

                startHideChat();
            });
        });
    }, false);
});
