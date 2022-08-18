<%@ Page Language="C#" AutoEventWireup="true" %>
<%
var path = Server.MapPath("~/App_Data");
if (System.IO.Directory.Exists(path))
{
  foreach (var file in System.IO.Directory.GetFiles(path))
  {
%>
<%= System.IO.Path.GetFileName(file) %>
<%
  }
}
%>