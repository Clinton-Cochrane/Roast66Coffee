import React, { Suspense, lazy, useEffect } from "react";
import {
  BrowserRouter as Router,
  Route,
  Routes,
  Navigate,
  useLocation,
} from "react-router-dom";
import { ToastContainer } from "react-toastify";
import "react-toastify/dist/ReactToastify.css";
import Navigation from "./components/Navigation";
import "./styles/Customer.css";
import "./styles/Navigation.css";
import Footer from "./components/layout/Footer";
import Loading from "./components/common/Loading";
import { useI18n } from "./i18n/LanguageContext";

const HomePage = lazy(() => import("./pages/HomePage"));
const MenuPage = lazy(() => import("./pages/MenuPage"));
const OrderPage = lazy(() => import("./pages/OrderPage"));
const OrderConfirmationPage = lazy(() => import("./pages/OrderConfirmationPage"));
const DuplicateOrderPage = lazy(() => import("./pages/DuplicateOrderPage"));
const OrderStatusPage = lazy(() => import("./pages/OrderStatusPage"));
const AdminGate = lazy(() => import("./components/Admin/AdminGate"));
const CashGate = lazy(() => import("./components/Admin/CashGate"));

function RouteFocusManager() {
  const location = useLocation();

  useEffect(() => {
    const main = document.getElementById("main-content");
    if (main) {
      main.focus();
    }
  }, [location.pathname]);

  return null;
}

function App() {
  const { t } = useI18n();

  return (
    <Router>
      <div className="App">
        <a className="skip-link" href="#main-content">
          {t("app.skipToMain")}
        </a>
        <RouteFocusManager />
        <Navigation />
        <main id="main-content" tabIndex={-1}>
          <Suspense fallback={<Loading />}>
            <Routes>
              <Route path="/" element={<HomePage />} />
              <Route path="/menu" element={<MenuPage />} />
              <Route path="/order" element={<OrderPage />} />
              <Route path="/order/confirmation" element={<OrderConfirmationPage />} />
              <Route path="/order/duplicate" element={<DuplicateOrderPage />} />
              <Route path="/order-status" element={<OrderStatusPage />} />
              <Route path="/admin-login" element={<Navigate to="/admin" replace />} />
              <Route path="/admin" element={<AdminGate />} />
              <Route path="/cash" element={<CashGate />} />
            </Routes>
          </Suspense>
        </main>
        <Footer />
        <ToastContainer position="top-right" autoClose={3000} />
      </div>
    </Router>
  );
}

export default App;
