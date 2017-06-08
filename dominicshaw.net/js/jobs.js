
function TT() {

    $('#job_intro').load('jobs/tt_intro.html');
    $('#job_out').load('jobs/tt_out.html');

    var chart = new CanvasJS.Chart("jobChart",
    {
        backgroundColor: null,

        data: [
            {
                indexLabelFontSize: 16,
                indexLabelFontColor: "white",
                indexLabelLineColor: "white",
                indexLabelPlacement: "outside",
                type: "doughnut",
                dataPoints: [
                    { y: 40, label: "WPF 40%", indexLabel: "WPF 40%" },
                    { y: 10, label: "ASP.NET 10%", indexLabel: "ASP.NET 10%" },
                    { y: 10, label: "SQL 10%", indexLabel: "SQL 10%" },
                    { y: 20, label: "WCF 20%", indexLabel: "WCF 20%" },
                    { y: 5, label: "Web API 5%", indexLabel: "Web API 5%" },
                    { y: 10, label: "Reporting (SSRS, DevExpress) 10%", indexLabel: "Reporting (SSRS, DevExpress) 10%" },
                    { y: 5, label: "Mobile, i.e. Xamarin & SPA 5%", indexLabel: "Mobile, i.e. Xamarin & SPA 5%" }
                ]
            }
        ]
    });

    chart.render();
}

function Nexus() {

    $('#job_intro').load('jobs/nexus_intro.html');
    $('#job_out').load('jobs/nexus_out.html');

    var chart = new CanvasJS.Chart("jobChart",
    {
        backgroundColor: null,

        data: [
            {
                indexLabelFontSize: 16,
                indexLabelFontColor: "white",
                indexLabelLineColor: "white",
                indexLabelPlacement: "outside",
                type: "doughnut",
                dataPoints: [
                    { y: 10, label: "WinForms 10%", indexLabel: "WinForms 10%" },
                    { y: 50, label: "ASP.NET 50%", indexLabel: "ASP.NET 50%" },
                    { y: 25, label: "SQL 25%", indexLabel: "SQL 25%" },
                    { y: 25, label: "Reporting (SSRS) 25%", indexLabel: "Reporting (SSRS) 25%" }
                ]
            }
        ]
    });

    chart.render();
}

function CMT() {

    $('#job_intro').load('jobs/cmt_intro.html');
    $('#job_out').load('jobs/cmt_out.html');

    var chart = new CanvasJS.Chart("jobChart",
    {
        backgroundColor: null,

        data: [
            {
                indexLabelFontSize: 16,
                indexLabelFontColor: "white",
                indexLabelLineColor: "white",
                indexLabelPlacement: "outside",
                type: "doughnut",
                dataPoints: [
                    { y: 35, label: "VB6 35%", indexLabel: "VB6 35%" },
                    { y: 5, label: ".NET WinForms 5%", indexLabel: ".NET WinForms 5%" },
                    { y: 20, label: "ASP 20%", indexLabel: "ASP 20%" },
                    { y: 5, label: "ASP.NET 5%", indexLabel: "ASP.NET 5%" },
                    { y: 25, label: "SQL 25%", indexLabel: "SQL 25%" },
                    { y: 10, label: "Reporting (Crystal) 10%", indexLabel: "Reporting (Crystal) 10%" }
                ]
            }
        ]
    });

    chart.render();
}

function PAA() {

    $('#job_intro').load('jobs/paa_intro.html');
    $('#job_out').load('jobs/paa_out.html');

    var chart = new CanvasJS.Chart("jobChart",
    {
        backgroundColor: null,

        data: [
            {
                indexLabelFontSize: 16,
                indexLabelFontColor: "white",
                indexLabelLineColor: "white",
                indexLabelPlacement: "outside",
                type: "doughnut",
                dataPoints: [
                    { y: 60, label: "PHP 60%", indexLabel: "PHP 60%" },
                    { y: 35, label: "MySql 35%", indexLabel: "MySql 35%" },
                    { y: 5, label: "ASP.NET 5%", indexLabel: "ASP.NET 5%" }
                ]
            }
        ]
    });

    chart.render();
}