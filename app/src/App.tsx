import { Routes, Route } from "react-router-dom";
import MainLayout from "./layouts/MainLayout";

import Home from "./pages/Home";
import Login from "./pages/Login";
import Shop from "./pages/Shop";
import Tournaments from "./pages/tournaments/Tournament";
import CreateTournament from "./pages/tournaments/CreateTournament";
import TournamentDetails from "./pages/tournaments/TournamentDetail";
import TournamentAdminDashboard from "./pages/tournaments/TournamentAdminDashboard";
import TournamentAdminTeams from "./pages/tournaments/TournamentAdminTeams";
import TournamentAdminStage from "./pages/tournaments/TournamentAdminStage";
import Profile from "./pages/Profile";
import NotificationTestPage from "./pages/admin/NotificationTest";

function App() {
  return (
    <Routes>
      {/* Pages AVEC header + footer */}
      <Route element={<MainLayout />}>
        <Route path="/" element={<Home />} />
        <Route path="/shop" element={<Shop />} />
        <Route path="/profile" element={<Profile />} />
        <Route path="/admin/test-notifications" element={<NotificationTestPage />} />

        <Route path="/tournaments" element={<Tournaments />} />
        <Route path="/tournaments/create" element={<CreateTournament />} />
        <Route path="/tournament/:id" element={<TournamentDetails />} />
        <Route
          path="/tournament/:id/admin"
          element={<TournamentAdminDashboard />}
        />
        <Route
          path="/tournament/:id/admin/teams"
          element={<TournamentAdminTeams />}
        />
        <Route
          path="/tournament/:id/admin/stage"
          element={<TournamentAdminStage />}
        />
      </Route>

      {/* Page SANS header + footer */}
      <Route path="/login" element={<Login />} />
    </Routes>
  );
}

export default App;
