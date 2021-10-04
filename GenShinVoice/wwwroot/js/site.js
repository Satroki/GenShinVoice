// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

let lastAudio;
let lastSrc;

function PlayAudio(src) {
    if (src == lastSrc && lastAudio) {
        if (lastAudio.paused || lastAudio.ended)
            lastAudio.play();
        else
            lastAudio.pause();
    } else {
        if (lastAudio && !lastAudio.ended)
            lastAudio.pause();
        lastSrc = src;
        lastAudio = new Audio(src);
        lastAudio.play();
    }
}