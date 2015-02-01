$(document).ready(function(){
    $("#signin.form").on('submit', function (e) {

        e.preventDefault();

        $.mobile.showPageLoadingMsg();

        $.post(this.attr('action'), this.serialize(), function(response){
            $.mobile.hidePageLoadingMsg();

            // bind some stuff to the dashboard
            $.mobile.changePage('#dashboard');

        }, 'json');
    });
});