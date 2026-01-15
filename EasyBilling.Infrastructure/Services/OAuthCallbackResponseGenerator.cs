using EasyBilling.Application.Interfaces.Services;

namespace EasyBilling.Presentation.Services;

public class OAuthCallbackResponseGenerator : IOAuthCallbackResponseGenerator
{
    public string GenerateSuccessResponse()
    {
        return @"
            <!DOCTYPE html>
            <html>
            <head>
                <title>ANAF Authorization</title>
                <style>
                    body { font-family: system-ui, sans-serif; display: flex; justify-content: center; align-items: center; height: 100vh; margin: 0; background: #f0fdf4; }
                    .container { text-align: center; padding: 2rem; }
                    .icon { font-size: 4rem; margin-bottom: 1rem; }
                    h1 { color: #166534; margin-bottom: 0.5rem; }
                    p { color: #6b7280; }
                </style>
            </head>
            <body>
                <div class='container'>
                    <div class='icon'>✓</div>
                    <h1>Authorization Successful</h1>
                    <p>This window will close automatically...</p>
                </div>
                <script>
                    if (window.opener) {
                        window.opener.postMessage({ type: 'ANAF_AUTH_SUCCESS' }, '*');
                        setTimeout(function() { window.close(); }, 1500);
                    }
                </script>
            </body>
            </html>";
    }

    public string GenerateErrorResponse(string errorMessage)
    {
        var sanitizedError = errorMessage
            .Replace("'", "\\'")
            .Replace("\"", "\\\"")
            .Replace("\n", " ")
            .Replace("\r", " ");

        return $@"
            <!DOCTYPE html>
            <html>
            <head>
                <title>ANAF Authorization Error</title>
                <style>
                    body {{ font-family: system-ui, sans-serif; display: flex; justify-content: center; align-items: center; height: 100vh; margin: 0; background: #fef2f2; }}
                    .container {{ text-align: center; padding: 2rem; max-width: 500px; }}
                    .icon {{ font-size: 4rem; margin-bottom: 1rem; }}
                    h1 {{ color: #dc2626; margin-bottom: 0.5rem; }}
                    p {{ color: #6b7280; }}
                    .error {{ background: #fee2e2; padding: 1rem; border-radius: 8px; margin-top: 1rem; color: #991b1b; font-size: 0.875rem; word-break: break-word; }}
                    button {{ margin-top: 1rem; padding: 0.5rem 1rem; background: #6b7280; color: white; border: none; border-radius: 4px; cursor: pointer; }}
                    button:hover {{ background: #4b5563; }}
                </style>
            </head>
            <body>
                <div class='container'>
                    <div class='icon'>✕</div>
                    <h1>Authorization Failed</h1>
                    <p>There was a problem connecting to ANAF.</p>
                    <div class='error'>{sanitizedError}</div>
                    <button onclick='window.close()'>Close Window</button>
                </div>
                <script>
                    if (window.opener) {{
                        window.opener.postMessage({{ type: 'ANAF_AUTH_ERROR', error: '{sanitizedError}' }}, '*');
                    }}
                </script>
            </body>
            </html>";
    }
}