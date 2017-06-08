function ContactMe() {
    var name = document.getElementById("yourName").value;
    var email = document.getElementById("yourEmail").value;
    var subj = document.getElementById("yourSubject").value;
    var msg = document.getElementById("yourMessage").value;

    if (name === '' || email === '' || subj === '' || msg === '') {
        alert('Ah, go on. Fill \'em all in.');
        return;
    }

    document.getElementById("sendEmail").value = 'Sending...';
    document.getElementById("sendEmail").disabled = 'disabled';

    $.ajax({
        type: "POST",
        url: "SendEmail.aspx",
        data: {
            name: name,
            email: email,
            subj: subj,
            msg: msg
        },
        contentType: "application/x-www-form-urlencoded; charset=utf-8",
        async: true,
        success: function (response) {
            document.getElementById("sendEmail").value = 'Email Sent!';
        },
        error: function (err) {
            document.getElementById("sendEmail").value = 'Email Failed :( ' + err.responseText;
        }
    });
}