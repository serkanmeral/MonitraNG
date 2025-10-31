import {
  ChartPieIcon,
  CoffeeIcon,
  CpuIcon,
  FlagIcon,
  BasketIcon,
  ApertureIcon,
  LayoutGridIcon,
  BoxIcon,
  Message2Icon,
  FilesIcon,
  CalendarIcon,
  UserCircleIcon,
  ChartBarIcon,
  ShoppingCartIcon,
  ChartLineIcon,
  ChartAreaIcon,
  ChartDotsIcon,
  ChartArcsIcon,
  ChartCandleIcon,
  ChartDonut3Icon,
  ChartRadarIcon,
  LayoutIcon,
  CardboardsIcon,
  PhotoIcon,
  FileTextIcon,
  BoxAlignBottomIcon,
  BoxAlignLeftIcon,
  FileDotsIcon,
  EditCircleIcon,
  AppsIcon,
  BorderAllIcon,
  BorderHorizontalIcon,
  BorderInnerIcon,
  BorderTopIcon,
  BorderVerticalIcon,
  BorderStyle2Icon,
  LoginIcon,
  UserPlusIcon,
  RotateIcon,
  ZoomCodeIcon,
  SettingsIcon,
  AlertCircleIcon,
  BrandTablerIcon,
  CodeAsterixIcon,
  BrandCodesandboxIcon,
  CircleDotIcon,
  ColumnsIcon,
  RowInsertBottomIcon,
  EyeTableIcon,
  SortAscendingIcon,
  PageBreakIcon,
  FilterIcon,
  BoxModelIcon,
  ServerIcon,
  JumpRopeIcon,
  LayoutKanbanIcon,
  PhotoAiIcon,
  SearchIcon,
  SocialIcon,
  BrandTidalIcon,
  AppWindowIcon,
  CurrencyDollarIcon,
  HelpIcon,
  FileCheckIcon,
  MailIcon,
  TicketIcon,
} from "vue-tabler-icons";

export interface asset {
  header?: string;
  title?: string;
  icon?: any;
  to?: string;
  chip?: string;
  chipBgColor?: string;
  chipColor?: string;
  chipVariant?: string;
  chipIcon?: string;
  children?: asset[];
  disabled?: boolean;
  type?: string;
  subCaption?: string;
}

const assetItem: asset[] = [
  { header: "Tünel İşletmeleri" },
  {
    title: "1. Bölge",
    icon: BoxModelIcon,
    chip: "2",
    chipColor: "surface",
    chipBgColor: "error", 
    to: "/",    
    children: [
      {
        title: "Çamlıca Tüneli",
        icon: BoxModelIcon,
        to: "/",
        chip: "3",
        chipColor: "surface",
        chipBgColor: "error",  
        subCaption:"Serkan",       
        children: [
          {
            title: "Scada 1",
            icon: PhotoAiIcon,
            to: "/",
            chip: "2",
            chipColor: "surface",
            chipBgColor: "secondary",            
          },
          {
            title: "Scada 2",
            icon: PhotoAiIcon,
            to: "/",
            chip: "1",
            chipColor: "surface",
            chipBgColor: "error",            
          }
        ],        
      }
    ],
  }
];

export default assetItem;
