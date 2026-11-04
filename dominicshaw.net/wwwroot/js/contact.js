function ContactMe() {
    var name = document.getElementById("yourName").value;
    var email = document.getElementById("yourEmail").value;
    var subj = document.getElementById("yourSubject").value;
    var msg = document.getElementById("yourMessage").value;
    var website = document.getElementById("yourWebsite").value;

    if (name === '' || email === '' || subj === '' || msg === '') {
        alert('Ah, go on. Fill \'em all in.');
        return;
    }

    var sendButton = document.getElementById("sendEmail");
    sendButton.value = 'Sending...';
    sendButton.disabled = 'disabled';

    var configuredApiBaseUrl = window.dominicShawConfig && window.dominicShawConfig.contactApiBaseUrl;
    var apiBaseUrl = configuredApiBaseUrl ? configuredApiBaseUrl.replace(/\/$/, "") : "";

    $.ajax({
        type: "POST",
        url: apiBaseUrl + "/api/contact",
        data: JSON.stringify({
            name: name,
            email: email,
            subject: subj,
            message: msg,
            website: website
        }),
        contentType: "application/json; charset=utf-8",
        success: function () {
            sendButton.value = 'Email Sent!';
        },
        error: function (err) {
            sendButton.value = 'Email Failed :( ' + err.responseText;
            sendButton.disabled = false;
        }
    });
}
