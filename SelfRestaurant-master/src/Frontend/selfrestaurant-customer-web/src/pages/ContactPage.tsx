import { SiteFooter } from "../components/SiteFooter";
import { PublicNavbar } from "../components/PublicNavbar";

const copy = {
  title: "Li\u00ean H\u1ec7 Self Restaurant",
  subtitle: "Ch\u00fang t\u00f4i lu\u00f4n s\u1eb5n s\u00e0ng l\u1eafng nghe \u00fd ki\u1ebfn t\u1eeb b\u1ea1n.",
  sectionTitle: "Th\u00f4ng tin li\u00ean h\u1ec7",
  intro: "N\u1ebfu b\u1ea1n c\u00f3 g\u00f3p \u00fd ho\u1eb7c c\u1ea7n h\u1ed7 tr\u1ee3, vui l\u00f2ng li\u00ean h\u1ec7:",
  email: "Email: support@selfrestaurant.local",
  phone: "\u0110i\u1ec7n tho\u1ea1i: 0123 456 789",
  address: "\u0110\u1ecba ch\u1ec9: 123 Nguy\u1ec5n Hu\u1ec7, Qu\u1eadn 1, TP.HCM",
} as const;

export function ContactPage() {
  return (
    <div className="home-page">
      <PublicNavbar />
      <header className="hero-section home-subpage-hero contact-hero-section">
        <div className="container">
          <h1>{copy.title}</h1>
          <p>{copy.subtitle}</p>
        </div>
      </header>

      <section className="section-container">
        <div className="container">
          <h2 className="section-title">{copy.sectionTitle}</h2>
          <div className="row justify-content-center">
            <div className="col-lg-6 text-start">
              <div className="info-page-copy info-page-copy-compact">
                <p className="mb-3">{copy.intro}</p>
                <ul className="contact-bullet-list">
                  <li>{copy.email}</li>
                  <li>{copy.phone}</li>
                  <li>{copy.address}</li>
                </ul>
              </div>
            </div>
          </div>
        </div>
      </section>

      <SiteFooter />
    </div>
  );
}
