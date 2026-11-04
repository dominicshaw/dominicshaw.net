const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');

const contactScript = fs.readFileSync(
    path.join(__dirname, '..', 'dominicshaw.net', 'wwwroot', 'js', 'contact.js'),
    'utf8');

test('a failed contact request can be retried', () => {
    const elements = {
        yourName: { value: 'Visitor' },
        yourEmail: { value: 'visitor@example.com' },
        yourSubject: { value: 'Azure migration' },
        yourMessage: { value: 'Please get in touch.' },
        yourWebsite: { value: '' },
        sendEmail: { value: 'send', disabled: false }
    };
    const requests = [];
    const context = {
        document: {
            getElementById: id => elements[id]
        },
        window: {
            dominicShawConfig: {
                contactApiBaseUrl: 'https://function.example'
            }
        },
        $: {
            ajax: options => requests.push(options)
        },
        alert: () => assert.fail('The valid test form should not show an alert.'),
        JSON
    };

    vm.runInNewContext(contactScript, context, { filename: 'contact.js' });

    context.ContactMe();
    assert.equal(requests.length, 1);
    assert.equal(elements.sendEmail.disabled, 'disabled');

    requests[0].error({ responseText: 'Service unavailable' });
    assert.equal(elements.sendEmail.disabled, false);

    context.ContactMe();
    assert.equal(requests.length, 2);
    assert.equal(elements.sendEmail.disabled, 'disabled');

    requests[1].success();
    assert.equal(elements.sendEmail.disabled, 'disabled');
    assert.equal(elements.sendEmail.value, 'Email Sent!');
});
