<%@ Page Language="C#" AutoEventWireup="true" Inherits="Rock.Web.UI.RockPage" %>

<!DOCTYPE html>

<html class="no-js">
<head runat="server">
  <title></title>

  <script src="<%# System.Web.Optimization.Scripts.Url("~/Scripts/Bundles/RockJQueryLatest" ) %>"></script>

  <!-- Theme Included CSS Files -->
  <!--link href="<%# ResolveRockUrl("~~/Styles/twcss/src/tailwind-base.css", true) %>" rel="stylesheet"-->
  <link href="<%# ResolveRockUrl("~~/Styles/twcss/src/tailwind.css", true) %>" rel="stylesheet">
  <link href="<%# ResolveRockUrl("~~/Styles/font-awesome.css", true) %>" rel="stylesheet">
  <link href="<%# ResolveRockUrl("~~/Styles/bootstrap.css", true) %>" rel="stylesheet">
  <link rel="stylesheet" href="<%# ResolveRockUrl("~~/Styles/theme.css", true) %>"/>

  <style>
    html, body {
      height: auto;
      width: 100vw;
      min-width: 100vw;
      background-color: #ffffff;
      margin: 0 0 0 0;
      padding: 0 0 0 0;
      vertical-align: top;
    }
  </style>

</head>

<body class="rock-blank">
  <form id="form1" runat="server">
    <asp:ScriptManager ID="sManager" runat="server" />

    <asp:UpdateProgress ID="updateProgress" runat="server" DisplayAfter="800">
      <ProgressTemplate>
        <div class="updateprogress-status">
          <div class="spinner">
            <div class="rect1"></div>
            <div class="rect2"></div>
            <div class="rect3"></div>
            <div class="rect4"></div>
            <div class="rect5"></div>
          </div>
        </div>
        <div class="updateprogress-bg modal-backdrop">
        </div>
      </ProgressTemplate>
    </asp:UpdateProgress>

    <main class="container-fluid">

      <!-- Start Content Area -->
      <Rock:Zone Name="Main" runat="server" />

    </main>
  </form>
</body>
</html>