<%@ Page Language="C#" AutoEventWireup="true" %>

<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">

<html xmlns="http://www.w3.org/1999/xhtml">
<head id="Head1" runat="server">
    <title></title>
</head>
<body>
    <form id="form1" runat="server">
    <div>
      <h1>Hello!</h1>

      <p>
        Application settings:
      </p>

      <table>
        <tr>
          <td>Key</td>
          <td>Value</td>
        </tr>
      <% 
        foreach (var key in ConfigurationManager.AppSettings.Keys)
        {
      %>
          <tr>
            <td><%= key %></td>
            <td><code><%= ConfigurationManager.AppSettings[key.ToString()] %></code></td>
          </tr>
      <%
        } 
      %>
    </div>
    </form>
</body>
</html>
