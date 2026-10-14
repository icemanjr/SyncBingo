import { BrowserRouter, Route, Routes } from 'react-router'
import { ComingNext } from './app/ComingNext'
import { FullScreen, Layout } from './app/Layout'
import { CreateGamePage } from './features/host/CreateGamePage'
import { HostConsole } from './features/host/HostConsole'
import { LandingPage } from './features/landing/LandingPage'

/** The site's pages (docs/06 §1). */
export function AppRoutes() {
  return (
    <Routes>
      <Route
        path="/"
        element={
          <Layout>
            <LandingPage />
          </Layout>
        }
      />
      <Route
        path="/host/new"
        element={
          <Layout>
            <CreateGamePage />
          </Layout>
        }
      />
      <Route
        path="/host/:code"
        element={
          <Layout>
            <HostConsole />
          </Layout>
        }
      />
      <Route
        path="/join"
        element={
          <Layout>
            <ComingNext what="Joining a game" />
          </Layout>
        }
      />
      <Route
        path="/join/:code"
        element={
          <Layout>
            <ComingNext what="Joining a game" />
          </Layout>
        }
      />
      <Route
        path="/play/:code"
        element={
          <Layout>
            <ComingNext what="Your card" />
          </Layout>
        }
      />
      <Route
        path="/display/:code"
        element={
          <FullScreen>
            <ComingNext what="The display view" />
          </FullScreen>
        }
      />
      <Route
        path="/tv"
        element={
          <FullScreen>
            <ComingNext what="Connecting a TV" />
          </FullScreen>
        }
      />
      <Route
        path="*"
        element={
          <Layout>
            <ComingNext what="That page" />
          </Layout>
        }
      />
    </Routes>
  )
}

export default function App() {
  return (
    <BrowserRouter>
      <AppRoutes />
    </BrowserRouter>
  )
}
