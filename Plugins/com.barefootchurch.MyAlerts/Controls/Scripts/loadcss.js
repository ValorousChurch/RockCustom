var loadCSS = function () {
    var relPath = '/plugins/com_barefootchurch/MyAlerts/styles/styles.css';
    var styleLink = $('<link>').attr('rel', 'stylesheet').attr('href', relPath);
    $('head').append(styleLink);
}();
