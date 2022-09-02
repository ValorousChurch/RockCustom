Sys.Application.add_load( function () {
    var $profilePhoto = $('.loginstatus .profile-photo' );
    var $myAlerts = $( '.my-alerts' );
    if ( $profilePhoto.length !== 0 && $myAlerts.length !== 0 ) {
        $myAlerts.css({
            right: $(window).width() - $profilePhoto.offset().left - $profilePhoto.outerWidth() - $myAlerts.outerWidth() / 2
        });
    }
});