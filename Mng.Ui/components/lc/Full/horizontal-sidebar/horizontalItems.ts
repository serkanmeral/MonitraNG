import {
  AppsIcon,
  BorderAllIcon,
  BrandTablerIcon,
  CircleDotIcon,
  ClipboardIcon,
  FileDescriptionIcon,
  HomeIcon,
  BrandAirtableIcon,
  PointIcon,
  AppWindowIcon,
} from "vue-tabler-icons";

export interface menu {
  header?: string;
  title?: string;
  icon?: any;
  to?: string;
  divider?: boolean;
  chip?: string;
  chipColor?: string;
  chipVariant?: string;
  chipIcon?: string;
  children?: menu[];
  disabled?: boolean;
  subCaption?: string;
  class?: string;
  extraclass?: string;
  type?: string;
}

const horizontalItems: menu[] = [
  {
    title: "Dashboard",
    icon: HomeIcon,
    to: "#",
    children: [
      {
        title: "Analytical",
        icon: CircleDotIcon,
        to: "/dashboards/analytical",
      },
      {
        title: "Classic",
        icon: CircleDotIcon,
        to: "/dashboards/classic",
      },
      {
        title: "Demographical",
        icon: CircleDotIcon,
        to: "/dashboards/demographical",
      },
      {
        title: "Minimal",
        icon: CircleDotIcon,
        to: "/dashboards/minimal",
      },
      {
        title: "eCommerce",
        icon: CircleDotIcon,
        to: "/dashboards/ecommerce",
      },
      {
        title: "Modern",
        icon: CircleDotIcon,
        to: "/dashboards/modern",
      },
    ],
  },

  {
    title: "Front Pages",
    icon: AppWindowIcon,
    to: "#",
    children: [
      {
        title: "Homepage",
        icon: CircleDotIcon,
        to: "/front-pages/homepage",
      },
      {
        title: "About Us",
        icon: CircleDotIcon,
        to: "/front-pages/about-us",
      },
      {
        title: "Blog",
        icon: CircleDotIcon,
        to: "/front-pages/blog/posts",
      },
      {
        title: "Blog Details",
        icon: CircleDotIcon,
        to: "/front-pages/blog/early-black-friday-amazon-deals-cheap-tvs-headphones",
      },
      {
        title: "Contact Us",
        icon: CircleDotIcon,
        to: "/front-pages/contact-us",
      },
      {
        title: "Portfolio",
        icon: CircleDotIcon,
        to: "/front-pages/portfolio",
      },
      {
        title: "Pricing",
        icon: CircleDotIcon,
        to: "/front-pages/pricing",
      },
    ],
  },

  {
    title: "Apps",
    icon: AppsIcon,
    to: "#",
    children: [
      {
        title: "Contact",
        icon: CircleDotIcon,
        to: "/apps/contacts",
      },
      {
        title: "Chats",
        icon: CircleDotIcon,
        to: "/apps/chats",
      },
      {
        title: "Blog",
        icon: CircleDotIcon,
        to: "/",
        children: [
          {
            title: "Posts",
            icon: PointIcon,
            to: "/apps/blog/posts",
          },
          {
            title: "Detail",
            icon: PointIcon,
            to: "/apps/blog/early-black-friday-amazon-deals-cheap-tvs-headphones",
          },
        ],
      },
      {
        title: "E-Commerce",
        icon: CircleDotIcon,
        to: "/ecommerce/",
        children: [
          {
            title: "Shop",
            icon: PointIcon,
            to: "/apps/ecommerce/products",
          },
          {
            title: "Detail",
            icon: PointIcon,
            to: "/apps/ecommerce/product/detail/1",
          },
          {
            title: "List",
            icon: PointIcon,
            to: "/apps/ecommerce/productlist",
          },
          {
            title: "Checkout",
            icon: PointIcon,
            to: "/apps/ecommerce/checkout",
          },
          {
            title: "Add Product",
            icon: CircleDotIcon,
            to: "/ecommerce/add-product",
          },
          {
            title: "Edit Product",
            icon: CircleDotIcon,
            to: "/ecommerce/edit-product",
          },
          {
            title: "Add Product",
            icon: CircleDotIcon,
            to: "/apps/ecommerce/addproduct",
          },
          {
            title: "Edit Product",
            icon: CircleDotIcon,
            to: "/apps/ecommerce/editproduct",
          },
        ],
      },
      {
        title: "User Profile",
        icon: CircleDotIcon,
        to: "/",
        children: [
          {
            title: "Profile",
            icon: PointIcon,
            to: "/apps/user/profile",
          },
          {
            title: "Followers",
            icon: PointIcon,
            to: "/apps/user/profile/followers",
          },
          {
            title: "Friends",
            icon: PointIcon,
            to: "/apps/user/profile/friends",
          },
          {
            title: "Gallery",
            icon: PointIcon,
            to: "/apps/user/profile/gallery",
          },
        ],
      },
      {
        title: "Invoice",
        icon: CircleDotIcon,
        to: "/",
        children: [
          {
            title: "List",
            icon: CircleDotIcon,
            to: "/apps/invoice",
          },
          {
            title: "Details",
            icon: CircleDotIcon,
            to: "/apps/invoice/details/102",
          },
          {
            title: "Create",
            icon: CircleDotIcon,
            to: "/apps/invoice/create",
          },
          {
            title: "Edit",
            icon: CircleDotIcon,
            to: "/apps/invoice/edit/102",
          },
        ],
      },
      {
        title: "Notes",
        icon: CircleDotIcon,
        to: "/apps/notes",
      },
      {
        title: "Calendar",
        icon: CircleDotIcon,
        to: "/apps/calendar",
      },
      {
        title: "Email",
        icon: CircleDotIcon,
        to: "/apps/email",
      },
      {
        title: "Tickets",
        icon: CircleDotIcon,
        to: "/apps/tickets",
      },
      {
        title: "Kanban",
        icon: CircleDotIcon,
        to: "/apps/kanban",
      },
    ],
  },

  {
    title: "Pages",
    icon: ClipboardIcon,
    to: "#",
    children: [
      {
        title: "Gallery Lightbox",
        icon: CircleDotIcon,
        to: "/theme-pages/gallery-lightbox",
      },
      {
        title: "Search Results",
        icon: CircleDotIcon,
        to: "/theme-pages/search-results",
      },
      {
        title: "Social Contacts",
        icon: CircleDotIcon,
        to: "/theme-pages/social-media-contacts",
      },
      {
        title: "Treeview",
        icon: CircleDotIcon,
        to: "/theme-pages/treeview",
      },
      {
        title: "Widget",
        icon: CircleDotIcon,
        to: "/widget-card",
        children: [
          {
            title: "Cards",
            icon: PointIcon,
            to: "/widgets/cards",
          },
          {
            title: "Banners",
            icon: PointIcon,
            to: "/widgets/banners",
          },
          {
            title: "Charts",
            icon: PointIcon,
            to: "/widgets/charts",
          },
        ],
      },
      {
        title: "UI",
        icon: CircleDotIcon,
        to: "#",
        children: [
          {
            title: "Alert",
            icon: PointIcon,
            to: "/ui-components/alert",
          },
          {
            title: "Accordion",
            icon: PointIcon,
            to: "/ui-components/accordion",
          },
          {
            title: "Avatar",
            icon: PointIcon,
            to: "/ui-components/avatar",
          },
          {
            title: "Chip",
            icon: PointIcon,
            to: "/ui-components/chip",
          },
          {
            title: "Dialog",
            icon: PointIcon,
            to: "/ui-components/dialogs",
          },
          {
            title: "List",
            icon: PointIcon,
            to: "/ui-components/list",
          },
          {
            title: "Menus",
            icon: PointIcon,
            to: "/ui-components/menus",
          },
          {
            title: "Rating",
            icon: PointIcon,
            to: "/ui-components/rating",
          },
          {
            title: "Tabs",
            icon: PointIcon,
            to: "/ui-components/tabs",
          },
          {
            title: "Tooltip",
            icon: PointIcon,
            to: "/ui-components/tooltip",
          },
          {
            title: "Typography",
            icon: PointIcon,
            to: "/ui-components/typography",
          },
        ],
      },
      {
        title: "Charts",
        icon: CircleDotIcon,
        to: "#",
        children: [
          {
            title: "Line",
            icon: PointIcon,
            to: "/charts/line-chart",
          },
          {
            title: "Gredient",
            icon: PointIcon,
            to: "/charts/gredient-chart",
          },
          {
            title: "Area",
            icon: PointIcon,
            to: "/charts/area-chart",
          },
          {
            title: "Candlestick",
            icon: PointIcon,
            to: "/charts/candlestick-chart",
          },
          {
            title: "Column",
            icon: PointIcon,
            to: "/charts/column-chart",
          },
          {
            title: "Doughnut & Pie",
            icon: PointIcon,
            to: "/charts/doughnut-pie-chart",
          },
          {
            title: "Radialbar & Radar",
            icon: PointIcon,
            to: "/charts/radialbar-chart",
          },
        ],
      },
      {
        title: "Auth",
        icon: CircleDotIcon,
        to: "#",
        children: [
          {
            title: "Error",
            icon: CircleDotIcon,
            to: "/auth/404",
          },
          {
            title: "Maintenance",
            icon: CircleDotIcon,
            to: "/auth/maintenance",
          },
          {
            title: "Login",
            icon: CircleDotIcon,
            to: "#",
            children: [
              {
                title: "Side Login",
                icon: PointIcon,
                to: "/auth/login",
              },
              {
                title: "Boxed Login",
                icon: PointIcon,
                to: "/auth/login2",
              },
            ],
          },
          {
            title: "Register",
            icon: CircleDotIcon,
            to: "#",
            children: [
              {
                title: "Side Register",
                icon: PointIcon,
                to: "/auth/register",
              },
              {
                title: "Boxed Register",
                icon: PointIcon,
                to: "/auth/register2",
              },
            ],
          },
          {
            title: "Forgot Password",
            icon: CircleDotIcon,
            to: "#",
            children: [
              {
                title: "Side Forgot Password",
                icon: PointIcon,
                to: "/auth/forgot-password",
              },
              {
                title: "Boxed Forgot Password",
                icon: PointIcon,
                to: "/auth/forgot-password2",
              },
            ],
          },
          {
            title: "Two Steps",
            icon: CircleDotIcon,
            to: "#",
            children: [
              {
                title: "Side Two Steps",
                icon: PointIcon,
                to: "/auth/two-step",
              },
              {
                title: "Boxed Two Steps",
                icon: PointIcon,
                to: "/auth/two-step2",
              },
            ],
          },
        ],
      },
    ],
  },

  {
    title: "Forms",
    icon: FileDescriptionIcon,
    to: "#",
    children: [
      {
        title: "Form Elements",
        icon: CircleDotIcon,
        to: "/components/",
        children: [
          {
            title: "Autocomplete",
            icon: PointIcon,
            to: "/forms/form-elements/autocomplete",
          },
          {
            title: "Combobox",
            icon: PointIcon,
            to: "/forms/form-elements/combobox",
          },
          {
            title: "Button",
            icon: PointIcon,
            to: "/forms/form-elements/button",
          },
          {
            title: "Checkbox",
            icon: PointIcon,
            to: "/forms/form-elements/checkbox",
          },
          {
            title: "Custom Inputs",
            icon: PointIcon,
            to: "/forms/form-elements/custominputs",
          },
          {
            title: "File Inputs",
            icon: PointIcon,
            to: "/forms/form-elements/fileinputs",
          },
          {
            title: "Radio",
            icon: PointIcon,
            to: "/forms/form-elements/radio",
          },
          {
            title: "Select",
            icon: PointIcon,
            to: "/forms/form-elements/select",
          },
          {
            title: "Date Time",
            icon: PointIcon,
            to: "/forms/form-elements/date-time",
          },
          {
            title: "Slider",
            icon: PointIcon,
            to: "/forms/form-elements/slider",
          },
          {
            title: "Switch",
            icon: PointIcon,
            to: "/forms/form-elements/switch",
          },

          {
            title: "Time Picker",
            icon: PointIcon,
            to: "/forms/form-elements/timepicker",
          },
          {
            title: "Stepper",
            icon: PointIcon,
            to: "/forms/form-elements/stepper",
          },
        ],
      },
      {
        title: "Form Layout",
        icon: CircleDotIcon,
        to: "/forms/form-layouts",
      },
      {
        title: "Form Horizontal",
        icon: CircleDotIcon,
        to: "/forms/form-horizontal",
      },
      {
        title: "Form Vertical",
        icon: CircleDotIcon,
        to: "/forms/form-vertical",
      },
      {
        title: "Form Custom",
        icon: CircleDotIcon,
        to: "/forms/form-custom",
      },
      {
        title: "Form Validation",
        icon: CircleDotIcon,
        to: "/forms/form-validation",
      },
    ],
  },
  {
    title: "Tables",
    icon: BorderAllIcon,
    to: "#",
    children: [
      {
        title: "Basic Table",
        icon: CircleDotIcon,
        to: "/tables/basic",
      },
      {
        title: "Dark Table",
        icon: CircleDotIcon,
        to: "/tables/dark",
      },
      {
        title: "Density Table",
        icon: CircleDotIcon,
        to: "/tables/density",
      },
      {
        title: "Fixed Header Table",
        icon: CircleDotIcon,
        to: "/tables/fixed-header",
      },
      {
        title: "Height Table",
        icon: CircleDotIcon,
        to: "/tables/height",
      },
      {
        title: "Editable Table",
        icon: CircleDotIcon,
        to: "/tables/editable",
      },
    ],
  },
  {
    title: "Data Tables",
    icon: BrandAirtableIcon,
    to: "#",
    children: [
      {
        title: "Basic Table",
        icon: CircleDotIcon,
        to: "/datatables/basic",
      },
      {
        title: "Header Table",
        icon: CircleDotIcon,
        to: "/datatables/headers",
      },
      {
        title: "Selection Table",
        icon: CircleDotIcon,
        to: "/datatables/Selectable",
      },
      {
        title: "Sorting Table",
        icon: CircleDotIcon,
        to: "/datatables/sorting",
      },
      {
        title: "Pagination Table",
        icon: CircleDotIcon,
        to: "/datatables/pagination",
      },
      {
        title: "Filtering Table",
        icon: CircleDotIcon,
        to: "/datatables/filtering",
      },
      {
        title: "Grouping Table",
        icon: CircleDotIcon,
        to: "/datatables/grouping",
      },
      {
        title: "Table Slots",
        icon: CircleDotIcon,
        to: "/datatables/slots",
      },
      {
        title: "CRUD Table",
        icon: CircleDotIcon,
        to: "/datatables/crudtable",
      },
    ],
  },
  {
    title: "Icons",
    icon: BrandTablerIcon,
    to: "#",
    children: [
      {
        title: "Material",
        icon: CircleDotIcon,
        to: "/icons/material",
      },
      {
        title: "Tabler",
        icon: CircleDotIcon,
        to: "/icons/tabler",
      },
    ],
  },
];

export default horizontalItems;
