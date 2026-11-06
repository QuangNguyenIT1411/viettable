// Branch map position typed by the admin (owner 06/11). Accepts "10.7769", "10,7769" and a pasted Google Maps pair
// "10.7769, 106.7009"; the server checks the same ranges again.
export type ParsedCoordinates = { latitude: number | null; longitude: number | null; error: string | null };

const number = (text: string) => {
  const value = text.trim().replace(",", ".");
  return /^-?\d{1,3}(\.\d{1,6})?$/.test(value) ? Number(value) : Number.NaN;
};

export function parseCoordinates(latitudeText: string, longitudeText: string): ParsedCoordinates {
  let lat = latitudeText.trim();
  let lng = longitudeText.trim();
  // A pair pasted into the first box: "10.776889, 106.700806".
  const pair = /^(-?\d{1,3}(?:\.\d+)?)\s*[,;\s]\s*(-?\d{1,3}(?:\.\d+)?)$/.exec(lat);
  if (pair && lng === "") { lat = pair[1]; lng = pair[2]; }
  if (lat === "" && lng === "") return { latitude: null, longitude: null, error: null };
  if (lat === "" || lng === "") return { latitude: null, longitude: null, error: "Cần nhập cả vĩ độ và kinh độ, hoặc để trống cả hai." };
  const latitude = number(lat);
  const longitude = number(lng);
  if (Number.isNaN(latitude) || Number.isNaN(longitude))
    return { latitude: null, longitude: null, error: "Toạ độ chỉ gồm số, tối đa 6 chữ số thập phân (ví dụ 10.776889)." };
  if (latitude < -90 || latitude > 90) return { latitude: null, longitude: null, error: "Vĩ độ phải từ -90 đến 90." };
  if (longitude < -180 || longitude > 180) return { latitude: null, longitude: null, error: "Kinh độ phải từ -180 đến 180." };
  if (latitude === 0 && longitude === 0) return { latitude: null, longitude: null, error: "Toạ độ 0, 0 không phải vị trí chi nhánh." };
  return { latitude, longitude, error: null };
}
