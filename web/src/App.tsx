import { BrowserRouter, Route, Routes } from 'react-router'
import { FullScreen, Layout } from './app/Layout'
import { NotFound } from './app/NotFound'
import { CreateGamePage } from './features/host/CreateGamePage'
import { HostConsole } from './features/host/HostConsole'
import { DisplayView } from './features/display/DisplayView'
import { PairPage } from './features/display/PairPage'
import { TvPage } from './features/display/TvPage'
import { FaqPage } from './features/guide/FaqPage'
import { HowToPlayPage } from './features/guide/HowToPlayPage'
import { LandingPage } from './features/landing/LandingPage'
import { PrivacyPage, TermsPage } from './features/legal/LegalPages'
import { JoinPage } from './features/play/JoinPage'
import { PlayScreen } from './features/play/PlayScreen'
import { SetEditor } from './features/sets/SetEditor'

/**
 * The site's pages (docs/06 §1). Only pages with content show ads (`Layout ads`, ADR 0012): the guides and the
 * game screens; forms, legal pages, the set editor, pairing and not found have none, and the display none at all.
 */
export function AppRoutes() {
  return (
    <Routes>
      <Route
        path="/"
        element={
          <Layout ads>
            <LandingPage />
          </Layout>
        }
      />
      <Route
        path="/how-to-play"
        element={
          <Layout ads>
            <HowToPlayPage />
          </Layout>
        }
      />
      <Route
        path="/faq"
        element={
          <Layout ads>
            <FaqPage />
          </Layout>
        }
      />
      <Route
        path="/terms"
        element={
          <Layout>
            <TermsPage />
          </Layout>
        }
      />
      <Route
        path="/privacy"
        element={
          <Layout>
            <PrivacyPage />
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
          <Layout wide ads>
            <HostConsole />
          </Layout>
        }
      />
      <Route
        path="/host/:code/set"
        element={
          <Layout>
            <SetEditor />
          </Layout>
        }
      />
      <Route
        path="/join"
        element={
          <Layout>
            <JoinPage />
          </Layout>
        }
      />
      <Route
        path="/join/:code"
        element={
          <Layout>
            <JoinPage />
          </Layout>
        }
      />
      <Route
        path="/play/:code"
        element={
          <Layout ads>
            <PlayScreen />
          </Layout>
        }
      />
      <Route
        path="/display/:code"
        element={
          <FullScreen>
            <DisplayView />
          </FullScreen>
        }
      />
      <Route
        path="/tv"
        element={
          <FullScreen>
            <TvPage />
          </FullScreen>
        }
      />
      <Route
        path="/pair/:pairingCode"
        element={
          <Layout>
            <PairPage />
          </Layout>
        }
      />
      <Route
        path="*"
        element={
          <Layout>
            <NotFound />
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
