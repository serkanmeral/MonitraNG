export interface assetView {
    header?: string;
    title?: string;
    icon?: any;
    to?: string;
    chip?: any;
    chipBgColor?: string;
    chipColor?: string;
    chipVariant?: string;
    chipIcon?: string;
    children?: assetView[];
    disabled?: boolean;
    type?: string;
    subCaption?: string;
  }