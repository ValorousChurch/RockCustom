Sys.Application.add_load( function () {
    var $profilePhoto = $('.loginstatus .profile-photo' );
    var $myAlerts = $( '.my-alerts' );
    if ( $profilePhoto.length !== 0 && $myAlerts.length !== 0 ) {
        $myAlerts.css({
            left: $profilePhoto.offset().left + $profilePhoto.width() - ($myAlerts.width() / 2),
        });
    }
});