import { Navigate, Route, Routes } from "react-router-dom";
import { AppShell } from "./app/app-shell.tsx";
import { GameWorkspaceView } from "./features/game/game-workspace-view.tsx";
import { HomeView } from "./features/home/home-view.tsx";
import { HostSetupView } from "./features/host/host-setup-view.tsx";
import { JoinRoomView } from "./features/join/join-room-view.tsx";
import { SettingsView } from "./features/settings/settings-view.tsx";
import { TemplateCatalogView } from "./features/templates/template-catalog-view.tsx";

export function App() {
  return (
    <AppShell>
      <Routes>
        <Route path="/" element={<HomeView />} />
        <Route path="/templates" element={<TemplateCatalogView />} />
        <Route path="/host/new" element={<HostSetupView />} />
        <Route path="/join" element={<JoinRoomView />} />
        <Route path="/game/:sessionId" element={<GameWorkspaceView />} />
        <Route path="/settings" element={<SettingsView />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </AppShell>
  );
}

export default App;
