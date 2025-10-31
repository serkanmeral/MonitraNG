import mock from "../../mockAdapter";
import { Chance } from "chance";
import type { assetView } from "@/types/apps/AssetTypes";
import { sub } from "date-fns";

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

const chance = new Chance();

const AssetViewData: assetView[] = [

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
             chip: null,
             chipColor: "surface",
             chipBgColor: "error",            
           }
         ],        
       }
     ],
   }
];

mock.onGet('/api/data/assetView/AssetViewData').reply(() => {
    return [200, AssetViewData];
});
export default AssetViewData;