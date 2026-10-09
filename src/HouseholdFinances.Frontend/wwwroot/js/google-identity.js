// Google Identity Services interop for the Blazor Server sign-in flow.
//
// The Google ID token can only be obtained in the browser, so this module loads the Google
// Identity Services script, initializes it with the configured client id, prompts the user, and
// resolves with the ID token (the credential). The Blazor Server host then exchanges that token
// for the API-issued pair server to server, so the API tokens never reach the browser.

const SCRIPT_ID = "google-identity-services";
const SCRIPT_SRC = "https://accounts.google.com/gsi/client";

let scriptPromise = null;

function loadScript() {
  if (scriptPromise) {
    return scriptPromise;
  }

  scriptPromise = new Promise((resolve, reject) => {
    if (window.google && window.google.accounts && window.google.accounts.id) {
      resolve();
      return;
    }

    const failLoad = () => {
      // Allow a later attempt to retry if the script could not be loaded.
      scriptPromise = null;
      reject(new Error("Google Identity Services failed to load."));
    };

    const existing = document.getElementById(SCRIPT_ID);
    if (existing) {
      existing.addEventListener("load", () => resolve());
      existing.addEventListener("error", failLoad);
      return;
    }

    const script = document.createElement("script");
    script.id = SCRIPT_ID;
    script.src = SCRIPT_SRC;
    script.async = true;
    script.defer = true;
    script.onload = () => resolve();
    script.onerror = failLoad;
    document.head.appendChild(script);
  });

  return scriptPromise;
}

// Prompts the user to sign in with Google and resolves with the ID token, or null when the user
// cancels or the prompt cannot be displayed. It rejects when the interop itself fails, which .NET
// surfaces as a JSException and reports through the shared client error convention.
export async function getGoogleIdToken(clientId) {
  if (!clientId) {
    throw new Error("The Google client id is not configured.");
  }

  await loadScript();

  if (!window.google || !window.google.accounts || !window.google.accounts.id) {
    throw new Error("Google Identity Services is unavailable.");
  }

  return await new Promise((resolve, reject) => {
    let settled = false;

    const settle = (value) => {
      if (!settled) {
        settled = true;
        resolve(value);
      }
    };

    const fail = (error) => {
      if (!settled) {
        settled = true;
        reject(error);
      }
    };

    window.google.accounts.id.initialize({
      client_id: clientId,
      callback: (response) => {
        if (response && response.credential) {
          settle(response.credential);
        } else {
          fail(new Error("Google sign-in returned no credential."));
        }
      },
      cancel_on_tap_outside: false,
      use_fedcm_for_prompt: true,
    });

    window.google.accounts.id.prompt((notification) => {
      if (!notification) {
        return;
      }

      try {
        const canceled =
          (notification.isNotDisplayed && notification.isNotDisplayed()) ||
          (notification.isSkippedMoment && notification.isSkippedMoment()) ||
          (notification.isDismissedMoment && notification.isDismissedMoment());

        if (canceled) {
          settle(null);
        }
      } catch {
        // Ignore notification inspection failures; the credential callback governs success.
      }
    });
  });
}
