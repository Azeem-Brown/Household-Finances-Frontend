// Google Identity Services interop for the Blazor Server sign-in flow.
//
// The Google ID token can only be obtained in the browser, so this module loads the Google
// Identity Services script and renders Google's own sign-in button into the page. When the user
// clicks that button Google invokes the credential callback, which this module resolves with. The
// Blazor Server host then exchanges that token for the API-issued pair server to server, so the API
// tokens never reach the browser.
//
// The rendered button flow is used instead of the One Tap prompt because the prompt is browser
// controlled and can be suppressed for reasons the application cannot observe or influence, while
// the button always starts the flow when the user clicks it. The button flow also removes the
// reliance on the prompt display-moment notifications (isNotDisplayed, isSkippedMoment,
// isDismissedMoment), which FedCM no longer populates.

const SCRIPT_ID = "google-identity-services";
const SCRIPT_SRC = "https://accounts.google.com/gsi/client";

// The container the button is rendered into. Login.razor renders an element with this id, so the id
// is the contract between the page and this module.
const BUTTON_CONTAINER_ID = "google-signin-button";

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

// Renders Google's sign-in button into the Login page container and resolves with the ID token
// when the user clicks it. It rejects when the script cannot be loaded, the button cannot be
// rendered, or Google returns no credential, which .NET surfaces as a JSException and reports
// through the shared client error convention.
export async function getGoogleIdToken(clientId) {
  if (!clientId) {
    throw new Error("The Google client id is not configured.");
  }

  await loadScript();

  if (
    !window.google ||
    !window.google.accounts ||
    !window.google.accounts.id ||
    typeof window.google.accounts.id.renderButton !== "function"
  ) {
    throw new Error("Google Identity Services is unavailable.");
  }

  const container = document.getElementById(BUTTON_CONTAINER_ID);
  if (!container) {
    throw new Error("The Google sign-in button container is not present on the page.");
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

    // Only the client id and the credential callback are needed for the button flow. The prompt
    // options (cancel_on_tap_outside, use_fedcm_for_prompt) are deliberately not set: the button
    // does not use the One Tap prompt or its display-moment notifications.
    window.google.accounts.id.initialize({
      client_id: clientId,
      callback: (response) => {
        if (response && response.credential) {
          settle(response.credential);
        } else {
          fail(new Error("Google sign-in returned no credential."));
        }
      },
    });

    try {
      window.google.accounts.id.renderButton(container, {
        type: "standard",
        theme: "outline",
        size: "large",
        text: "signin_with",
        shape: "rectangular",
        logo_alignment: "left",
      });
    } catch (error) {
      fail(
        error instanceof Error
          ? error
          : new Error("The Google sign-in button could not be rendered."),
      );
    }
  });
}
