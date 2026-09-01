using Microsoft.Identity.Client;

namespace VerisFlow.Mcp.Client.Sample;

/// <summary>
/// Factory providing standardized SystemWebViewOptions with responsive UI and loopback connection reset protection.
/// </summary>
public static class MsalSystemWebViewOptionsFactory
{
    /// <summary>
    /// Creates a configured <see cref="SystemWebViewOptions"/> instance containing clean HTML templates.
    /// </summary>
    /// <returns>A configured instance of <see cref="SystemWebViewOptions"/>.</returns>
    public static SystemWebViewOptions Create()
    {
        return new SystemWebViewOptions
        {
            HtmlMessageSuccess = GetSuccessHtml(),
            HtmlMessageError = GetErrorHtml()
        };
    }

    private static string GetSuccessHtml() => @"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'/>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'/>
    <title>Authentication Complete</title>
    <!-- Inline empty favicon prevents browser favicon requests on closed loopback socket -->
    <link rel='icon' href='data:,'/>
    <style>
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
            margin: 0;
            padding: 40px 20px;
            background-color: #1E1E1E;
            color: #D4D4D4;
            text-align: center;
        }
        .container {
            max-width: 440px;
            margin: 60px auto;
            padding: 32px 24px;
            background-color: #252526;
            border-radius: 8px;
            border: 1px solid #3E3E42;
        }
        h2 {
            color: #4EC9B0;
            font-size: 20px;
            margin: 0 0 12px 0;
            font-weight: 500;
        }
        p {
            color: #CCCCCC;
            font-size: 14px;
            line-height: 1.6;
            margin: 0;
        }
    </style>
</head>
<body>
    <div class='container'>
        <h2>Authentication Complete</h2>
        <p>You can return to the application.<br/>You may safely close this browser tab.</p>
    </div>
</body>
</html>";

    private static string GetErrorHtml() => @"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'/>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'/>
    <title>Authentication Failed</title>
    <link rel='icon' href='data:,'/>
    <style>
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
            margin: 0;
            padding: 40px 20px;
            background-color: #1E1E1E;
            color: #D4D4D4;
            text-align: center;
        }
        .container {
            max-width: 440px;
            margin: 60px auto;
            padding: 32px 24px;
            background-color: #252526;
            border-radius: 8px;
            border: 1px solid #3E3E42;
        }
        h2 {
            color: #F48771;
            font-size: 20px;
            margin: 0 0 12px 0;
            font-weight: 500;
        }
        p {
            color: #CCCCCC;
            font-size: 14px;
            line-height: 1.6;
            margin: 0;
        }
    </style>
</head>
<body>
    <div class='container'>
        <h2>Authentication Failed</h2>
        <p>An error occurred during authentication.<br/>Please return to the application and try again.</p>
    </div>
</body>
</html>";
}