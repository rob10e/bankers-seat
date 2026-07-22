import type { CapacitorConfig } from "@capacitor/cli";

const config: CapacitorConfig = {
  appId: "com.bankersseat.app",
  appName: "Banker's Seat",
  webDir: "../web/dist",
  plugins: {
    App: {
      disableBackButtonHandler: false,
    },
  },
  server: {
    androidScheme: "https",
  },
};

export default config;
