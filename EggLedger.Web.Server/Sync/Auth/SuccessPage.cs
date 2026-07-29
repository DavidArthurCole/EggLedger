namespace EggLedger.Web.Server.Sync.Auth;

public static class SuccessPage {
    public const string Html =
"""
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<meta http-equiv="refresh" content="2; url=/">
<title>Authentication Successful</title>
<style>
  *{margin:0;padding:0;box-sizing:border-box}
  body{min-height:100vh;display:flex;align-items:center;justify-content:center;
    background:#1a1a2e;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif}
  .card{background:#16213e;border:1px solid #0f3460;border-radius:12px;
    padding:48px 40px;text-align:center;max-width:380px;width:90%}
  .check{width:64px;height:64px;background:#22c55e;border-radius:50%;
    display:flex;align-items:center;justify-content:center;
    margin:0 auto 24px;font-size:28px;color:#fff}
  h1{color:#f1f5f9;font-size:22px;font-weight:600;margin-bottom:12px}
  p{color:#94a3b8;font-size:14px;line-height:1.6}
  a{color:#60a5fa;text-decoration:none;font-weight:600}
  a:hover{text-decoration:underline}
</style>
</head>
<body>
<div class="card">
  <div class="check">&#10003;</div>
  <h1>Authentication Successful</h1>
  <p>You're all set. Redirecting you back to EggLedger&hellip;<br><a href="/">Return now</a></p>
</div>
<script>setTimeout(function(){location.href="/"},2000)</script>
</body>
</html>
""";
}
