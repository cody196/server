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
        return;

        if (chatHideTimeout)
        {
            clearTimeout(chatHideTimeout);
        }

        chatHideTimeout = setTimeout(function()
        {
            $('#chat').fadeOut(200);
        }, 10000);
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

    var getLock = 0;

    function refetchData()
    {
        getLock = 0;

        $.get('http://chat/getNew', function(data)
        {
            if (getLock > 1)
            {
                setTimeout(refetchData, 50);

                return;
            }

            getLock++;

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

                var nameStr = '';

                if (name != '')
                {
                    nameStr = '<strong style="color: rgb(' + colorR + ', ' + colorG + ', ' + colorB + ')">' + name + ': </strong>';
                }

                buf.find('ul').append('<li>' + nameStr + message + '</li>');
                buf.scrollTop(buf[0].scrollHeight - buf.height());

                $('#chat').show(0);

                startHideChat();
            });
        });
    }

    // on poll, request this
    //registerPollFunction(function()
    window.addEventListener('message', function(event)
    {
        if (event.data.type != 'poll')
        {
            return;
        }

        refetchData();
    }, false);
});
