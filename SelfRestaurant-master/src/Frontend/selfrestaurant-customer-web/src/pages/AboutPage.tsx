import { SiteFooter } from "../components/SiteFooter";
import { PublicNavbar } from "../components/PublicNavbar";

const copy = {
  title: "V\u1ec1 Self Restaurant",
  subtitle: "Kh\u00e1m ph\u00e1 c\u00e2u chuy\u1ec7n v\u00e0 s\u1ee9 m\u1ec7nh c\u1ee7a ch\u00fang t\u00f4i.",
  sectionTitle: "C\u00e2u chuy\u1ec7n c\u1ee7a ch\u00fang t\u00f4i",
} as const;

export function AboutPage() {
  return (
    <div className="home-page">
      <PublicNavbar />
      <header className="hero-section home-subpage-hero about-hero-section">
        <div className="container">
          <h1>{copy.title}</h1>
          <p>{copy.subtitle}</p>
        </div>
      </header>

      <section className="section-container">
        <div className="container">
          <h2 className="section-title">{copy.sectionTitle}</h2>
          <div className="row justify-content-center">
            <div className="col-lg-8 text-start">
              <div className="info-page-copy info-page-copy-wide">
                <p>
                  {"Self Restaurant l\u00e0 h\u1ec7 th\u1ed1ng nh\u00e0 h\u00e0ng t\u1ef1 ph\u1ee5c v\u1ee5 v\u1edbi m\u1ee5c ti\u00eau mang l\u1ea1i tr\u1ea3i nghi\u1ec7m nhanh ch\u00f3ng, ti\u1ec7n l\u1ee3i v\u00e0 hi\u1ec7n \u0111\u1ea1i cho kh\u00e1ch h\u00e0ng."}
                </p>
                <p>
                  {"D\u1ef1 \u00e1n n\u00e0y \u0111\u01b0\u1ee3c x\u00e2y d\u1ef1ng ph\u1ee5c v\u1ee5 m\u1ee5c \u0111\u00edch h\u1ecdc t\u1eadp / demo, bao g\u1ed3m \u0111\u1ea7y \u0111\u1ee7 lu\u1ed3ng kh\u00e1ch h\u00e0ng \u0111\u1eb7t m\u00f3n, b\u1ebfp ch\u1ebf bi\u1ebfn v\u00e0 thu ng\u00e2n thanh to\u00e1n."}
                </p>
                <p>
                  {"Ki\u1ebfn tr\u00fac h\u1ec7 th\u1ed1ng s\u1eed d\u1ee5ng ASP.NET MVC, Entity Framework v\u00e0 giao di\u1ec7n Bootstrap, m\u00f4 ph\u1ecfng m\u00f4i tr\u01b0\u1eddng v\u1eadn h\u00e0nh th\u1ef1c t\u1ebf trong nh\u00e0 h\u00e0ng hi\u1ec7n \u0111\u1ea1i."}
                </p>
              </div>
            </div>
          </div>
        </div>
      </section>

      <SiteFooter />
    </div>
  );
}
