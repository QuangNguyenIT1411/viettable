package com.restaurant.viettable.ui.auth;

final class GoogleSignInRequestGuard {
    private boolean inFlight;

    synchronized boolean tryBegin() {
        if (inFlight) return false;
        inFlight = true;
        return true;
    }

    synchronized boolean isInFlight() {
        return inFlight;
    }

    synchronized void finish() {
        inFlight = false;
    }
}
