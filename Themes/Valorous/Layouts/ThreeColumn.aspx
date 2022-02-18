<%@ Page Language="C#" MasterPageFile="Site.Master" AutoEventWireup="true" Inherits="Rock.Web.UI.RockPage" %>
<%@ Import Namespace="Rock" %>
<%@ Import Namespace="Rock.Model" %>
<%@ Import Namespace="Rock.Web.UI" %>
<%@ Import Namespace="Rock.Web.Cache" %>

<asp:Content ID="ctMain" ContentPlaceHolderID="main" runat="server">

  <!-- Start Content Area -->
  <%-- <asp:PlaceHolder runat="server">
    <% var page = PageCache.Get( ( ( RockPage ) Page ).PageId ); %>
    <% var showPageTitle = page.PageDisplayTitle; %>
    <% if(showPageTitle) { %>
      <header class="page bg-primary exclude-nav">
        <div class="fluid-container">
          <div class="row header-content">
            <div class="col-lg-12 header-content-inner">
              <!-- Page Title -->
              <h1 class="section-heading"><Rock:PageIcon ID="PageIcon" runat="server" /> <Rock:PageTitle ID="PageTitle" runat="server" /></h1>
            </div>
          </div>
        </div>
      </header>
    <% } else { %>
      <header class="exclude-nav"></header>
    <% } %>
  </asp:PlaceHolder> --%>

  <Rock:Zone Name="SubNav" runat="server" />

  <asp:PlaceHolder runat="server">
    <% var page = PageCache.Get( ( ( RockPage ) Page ).PageId ); %>
    <% var showPageBreadcrumbs = page.PageDisplayBreadCrumb; %>
    <% if(showPageBreadcrumbs) { %>
      <div class="breadcrumb-container">
        <Rock:PageBreadCrumbs ID="PageBreadCrumbs" runat="server" />
      </div>
    <% } %>
  </asp:PlaceHolder>

  <!-- Start Content Area -->
  <main class="container" style="margin: 50px auto;">

    <!-- Ajax Error -->
    <div class="alert alert-danger ajax-error no-index" style="display:none">
      <p><strong>Error</strong></p>
      <span class="ajax-error-message"></span>
    </div>

    <div class="row">
      <div class="col-md-12">
        <Rock:Zone Name="Feature" runat="server" />
      </div>
    </div>

    <div class="row">
      <div class="col-md-4">
        <Rock:Zone Name="Sidebar 1" runat="server" />
      </div>
      <div class="col-md-4">
        <Rock:Zone Name="Main" runat="server" />
      </div>
      <div class="col-md-4">
        <Rock:Zone Name="Sidebar 2" runat="server" />
      </div>
    </div>

    <div class="row">
      <div class="col-md-12">
        <Rock:Zone Name="Section A" runat="server" />
      </div>
    </div>

    <div class="row">
      <div class="col-md-4">
        <Rock:Zone Name="Section B" runat="server" />
      </div>
      <div class="col-md-4">
        <Rock:Zone Name="Section C" runat="server" />
      </div>
      <div class="col-md-4">
        <Rock:Zone Name="Section D" runat="server" />
      </div>
    </div>

    <!-- End Content Area -->

  </main>

</asp:Content>
