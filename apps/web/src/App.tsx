import { Navigate, Route, Routes, useNavigate, useParams } from "react-router-dom";
import { useEffect } from "react";
import { getSessionUserId } from "./utils/session-storage.ts";
import { AppShell } from "./app/app-shell.tsx";
import { GameWorkspaceView } from "./features/game/game-workspace-view.tsx";
import { HomeView } from "./features/home/home-view.tsx";
import { HostSetupView } from "./features/host/host-setup-view.tsx";
import { JoinRoomView } from "./features/join/join-room-view.tsx";
import { SettingsView } from "./features/settings/settings-view.tsx";
import { TemplateCatalogView } from "./features/templates/template-catalog-view.tsx";
import { TemplateEditorScreen } from "./features/templates/template-editor-screen.tsx";
import { useUiSessionStore } from "./features/session/ui-session-store.ts";
import { useCrashReporting, useDeepLink } from "./hooks/index.ts";
import { InstallPrompt } from "./pwa/install-prompt.tsx";
import { OfflineIndicator, UpdatePrompt } from "./pwa/offline-indicator.tsx";

function TemplateEditorRouteWrapper() {
  const { templateId, editionId, templateVersion } = useParams<{
    templateId: string;
    editionId: string;
    templateVersion: string;
  }>();

  return (
    <TemplateEditorScreen
      templateId={templateId}
      editionId={editionId}
      templateVersion={templateVersion}
    />
  );
}

export function App() {
  const navigate = useNavigate();
  const setRoomCodeDraft = useUiSessionStore((state) => state.setRoomCodeDraft);

  // Initialize session user ID on app load
  useEffect(() => {
    getSessionUserId();
  }, []);

  useCrashReporting({ apiKey: "console" });
  useDeepLink({
    onJoinLink: (roomCode) => {
      setRoomCodeDraft(roomCode);
      navigate(`/join/${roomCode}`);
    },
  });

  return (
    <AppShell>
      <InstallPrompt />
      <OfflineIndicator />
      <UpdatePrompt />
      <Routes>
        <Route path="/" element={<HomeView />} />
        <Route path="/templates" element={<TemplateCatalogView />} />
        <Route
          path="/templates/edit/:templateId/:editionId/:templateVersion"
          element={<TemplateEditorRouteWrapper />}
        />
        <Route path="/host/new" element={<HostSetupView />} />
        <Route path="/join" element={<JoinRoomView />} />
        <Route path="/join/:roomCode" element={<JoinRoomView />} />
        <Route path="/game/:sessionId" element={<GameWorkspaceView />} />
        <Route path="/settings" element={<SettingsView />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </AppShell>
  );
}

export default App;
