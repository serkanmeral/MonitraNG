import { defineStore } from "pinia";
import axios from "@/utils/axios";
import { uniqueId } from "lodash";
import { sub } from "date-fns";
import { fetchData } from "@/services/apiService";

interface AssetViewType {
  assets: any[];
  selectedAsset: any | null; // Add this line to hold the selected email
}

export const useAssetStore = defineStore({
  id: "asset",
  state: (): AssetViewType => ({
    assets: [],
    selectedAsset: null, // Initialize as null
  }),
  actions: {
    async fetchAssets() {
      // try {

      fetchData("/api/v1/data/assets", "GET")
        .then((data:any) => {
          this.assets = data.data;          
        })
        .catch((err) => {
          console.log(err);
        });
    },
    selectAsset(asset: any) {
      // Update the method to accept an asset object
      this.selectedAsset = asset; // Store the selected asset
    },
    deleteAsset(id: number) {
      this.assets = this.assets.filter((asset) => asset.id !== id);
      this.selectedAsset = null; // Clear selected asset after deletion
    },
  },
});
