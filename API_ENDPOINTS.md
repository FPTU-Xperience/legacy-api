# 📡 TÀI LIỆU API ENDPOINTS TOÀN BỘ HỆ THỐNG CLUBREPORTHUB

> **Dành cho Frontend Developer:** Tài liệu hướng dẫn cấu hình Base URL và toàn bộ các API Endpoints của hệ thống Backend Microservices. Tuyệt đối không gọi trực tiếp vào các port nội bộ của các microservice con, **toàn bộ request bắt buộc phải đi qua API Gateway**.

---

## ⚙️ HƯỚNG DẪN CẤU HÌNH BASE URL CHO FRONTEND

Trong mã nguồn Frontend (React, Vue, Vite, Next.js), bạn chỉ cần cấu hình **duy nhất 1 biến môi trường Base URL** trỏ vào cổng **API Gateway (Port 7000)**:

### 1. Khi chạy thử nghiệm tại Local (Localhost):
```env
# .env hoặc .env.development (Frontend)
VITE_API_BASE_URL=http://localhost:7000
# Hoặc nếu dùng Create-React-App:
REACT_APP_API_BASE_URL=http://localhost:7000
```

### 2. Khi Deploy lên VPS hoặc Máy Thầy (Server IP):
> Thay thế `192.168.1.100` bằng địa chỉ IP thật của máy chủ hoặc tên miền:
```env
# .env.production (Frontend)
VITE_API_BASE_URL=http://192.168.1.100:7000
# Hoặc nếu đã trỏ tên miền và cài SSL:
VITE_API_BASE_URL=https://api.yourdomain.com
```

### 3. Cấu hình Axios Instance mẫu (Frontend):
```javascript
import axios from 'axios';

const api = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || 'http://localhost:7000',
  headers: {
    'Content-Type': 'application/json',
  },
});

// Tự động gắn Bearer JWT Token vào Header cho mọi request
api.interceptors.request.use((config) => {
  const token = localStorage.getItem('accessToken');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

export default api;
```

---

## 📑 DANH SÁCH TOÀN BỘ API ENDPOINTS (KHÔNG THIẾU CÁI NÀO)

---

### 1. XÁC THỰC & TÀI KHOẢN (AUTHENTICATION & USERS)
* **Base Path:** `{{baseUrl}}/api/auth` và `{{baseUrl}}/api/users`

| Method | Endpoint | Quyền hạn | Mô tả | Request Body / Query Params |
| :--- | :--- | :--- | :--- | :--- |
| `POST` | `/api/auth/login` | Public | Đăng nhập hệ thống, lấy JWT & RefreshToken | `{"usernameOrEmail": "", "password": ""}` |
| `POST` | `/api/auth/register` | Public | Đăng ký tài khoản sinh viên mới | `{"username": "", "password": "", "email": "", "fullName": ""}` |
| `POST` | `/api/auth/refresh` | Public | Cấp lại AccessToken mới bằng RefreshToken | `{"refreshToken": ""}` |
| `POST` | `/api/auth/logout` | Authenticated | Đăng xuất (Thu hồi refresh token) | `{"refreshToken": ""}` |
| `GET` | `/api/users` | Admin, SystemAdmin | Danh sách toàn bộ người dùng (Phân trang, tìm kiếm) | `?search=&role=&page=1&pageSize=20` |
| `POST` | `/api/users` | Admin, SystemAdmin | Tạo tài khoản người dùng mới (gán vai trò) | `{"username": "", "email": "", "fullName": "", "password": "", "roles": ["ClubManager"]}` |
| `PUT` | `/api/users/{id}` | Admin, SystemAdmin | Cập nhật thông tin người dùng & vai trò | `{"fullName": "", "email": "", "roles": ["Treasurer"]}` |
| `PATCH` | `/api/users/{id}/lock` | Admin, SystemAdmin | Khóa tài khoản người dùng | *(Không có body)* |
| `PATCH` | `/api/users/{id}/unlock` | Admin, SystemAdmin | Mở khóa tài khoản người dùng | *(Không có body)* |
| `GET` | `/api/roles` | Admin, SystemAdmin | Danh sách các quyền/vai trò trong hệ thống | *(Không có body)* |
| `POST` | `/api/roles` | Admin, SystemAdmin | Thêm vai trò mới | `{"name": "Moderator", "description": ""}` |

---

### 2. QUẢN LÝ CÂU LẠC BỘ (CLUBS & MEMBERSHIPS)
* **Base Path:** `{{baseUrl}}/api/clubs`

| Method | Endpoint | Quyền hạn | Mô tả | Request Body / Query Params |
| :--- | :--- | :--- | :--- | :--- |
| `GET` | `/api/clubs` | Public / Auth | Danh sách các CLB đang hoạt động | `?category=&isActive=true&search=` |
| `GET` | `/api/clubs/{id}` | Public / Auth | Chi tiết thông tin một CLB | *(Param `id`)* |
| `POST` | `/api/clubs` | Admin | Tạo trực tiếp một CLB mới | `{"code": "F-CODE", "name": "", "category": "", "description": "", "contactEmail": ""}` |
| `PUT` | `/api/clubs/{id}` | Admin, ClubManager | Chỉnh sửa thông tin CLB | `{"name": "", "category": "", "description": "", "logoUrl": "", "contactEmail": ""}` |
| `DELETE` | `/api/clubs/{id}` | Admin | Xóa bỏ một CLB | *(Param `id`)* |
| `GET` | `/api/clubs/my-memberships` | Authenticated | Lấy danh sách các CLB mà user hiện tại đang tham gia | *(Không có body)* |

#### Quản lý thành viên trong CLB:
| Method | Endpoint | Quyền hạn | Mô tả | Request Body / Query Params |
| :--- | :--- | :--- | :--- | :--- |
| `POST` | `/api/clubs/{id}/join` | Authenticated | Nộp đơn xin gia nhập CLB | `{"fullName": "", "email": "", "phoneNumber": "", "reason": "", "skills": "", "expectations": ""}` |
| `GET` | `/api/clubs/{id}/memberships` | ClubManager, Admin | Danh sách đơn xin vào CLB cần duyệt | `?status=Pending` |
| `POST` | `/api/clubs/memberships/{membershipId}/approve` | ClubManager, Admin | Phê duyệt sinh viên vào CLB | `{"reviewNote": "Đồng ý"}` |
| `POST` | `/api/clubs/memberships/{membershipId}/reject` | ClubManager, Admin | Từ chối đơn xin vào CLB | `{"reviewNote": "Chưa phù hợp"}` |
| `GET` | `/api/clubs/{clubId}/members` | ClubMember, Manager | Danh sách thành viên chính thức của CLB | `?search=&role=&page=1&pageSize=20` |
| `GET` | `/api/clubs/{clubId}/members/{memberId}` | ClubMember, Manager | Chi tiết hồ sơ 1 thành viên | *(Param `clubId`, `memberId`)* |
| `DELETE` | `/api/clubs/{clubId}/members/{memberId}` | ClubManager, Admin | Mời ra khỏi CLB (Xóa thành viên) | *(Param `clubId`, `memberId`)* |
| `POST` | `/api/clubs/{id}/treasurers` | ClubManager, Admin | Bổ nhiệm thành viên làm Thủ quỹ (Treasurer) | `{"userId": 12}` |
| `POST` | `/api/clubs/memberships/{membershipId}/member` | ClubManager, Admin | Thu hồi quyền Thủ quỹ, chuyển về thành viên | *(Param `membershipId`)* |
| `GET` | `/api/clubs/{clubId}/member-roster` | ClubManager | Danh sách kiểm tra đối soát thành viên | *(Param `clubId`)* |
| `POST` | `/api/clubs/{clubId}/member-roster/resolve` | ClubManager | Đồng bộ giải quyết danh sách thành viên | `{"memberUserIds": [1, 2, 3]}` |

#### Quy trình Thành lập, Giải thể & Chuyển giao Chủ nhiệm CLB:
| Method | Endpoint | Quyền hạn | Mô tả | Request Body / Query Params |
| :--- | :--- | :--- | :--- | :--- |
| `POST` | `/api/clubs/applications` | Authenticated | Nộp đơn xin thành lập CLB mới | `{"code": "", "name": "", "category": "", "purpose": "", "mainActivities": "", "foundingMembersJson": ""}` |
| `GET` | `/api/clubs/applications` | Admin, StudentAffairs | Danh sách các đơn xin mở CLB | `?status=Pending` |
| `GET` | `/api/clubs/applications/{id}` | Admin, StudentAffairs | Chi tiết đơn xin mở CLB | *(Param `id`)* |
| `POST` | `/api/clubs/applications/{id}/approve` | Admin, StudentAffairs | Phê duyệt thành lập CLB | `{"reviewNote": "", "conditions": ""}` |
| `POST` | `/api/clubs/applications/{id}/reject` | Admin, StudentAffairs | Từ chối đơn thành lập CLB | `{"reviewNote": "Lý do từ chối"}` |
| `POST` | `/api/clubs/{clubId}/disband` | ClubManager | Nộp đơn xin giải thể CLB | `{"reason": "", "handoverPlan": ""}` |
| `GET` | `/api/clubs/{clubId}/disband-request`| ClubManager, Admin | Xem đơn xin giải thể của CLB | *(Param `clubId`)* |
| `GET` | `/api/clubs/disband-requests` | Admin, StudentAffairs | Danh sách tất cả đơn xin giải thể | `?status=Pending` |
| `POST` | `/api/clubs/disband-requests/{requestId}/approve` | Admin, StudentAffairs | Phê duyệt giải thể CLB | `{"reviewNote": ""}` |
| `POST` | `/api/clubs/disband-requests/{requestId}/reject` | Admin, StudentAffairs | Bác đơn giải thể | `{"reviewNote": ""}` |
| `GET` | `/api/clubs/{clubId}/members-for-transfer` | ClubManager | Lấy danh sách thành viên đủ điều kiện nhận chuyển giao | *(Param `clubId`)* |
| `POST` | `/api/clubs/{clubId}/transfer-ownership` | ClubManager | Nộp yêu cầu chuyển nhượng chức Chủ nhiệm | `{"targetUserId": 15, "reason": ""}` |
| `GET` | `/api/clubs/{clubId}/transfer-request` | ClubManager, Admin | Xem yêu cầu chuyển giao của CLB | *(Param `clubId`)* |
| `GET` | `/api/clubs/transfer-requests` | Admin, StudentAffairs | Danh sách đơn xin chuyển nhượng chức vụ | `?status=Pending` |
| `POST` | `/api/clubs/transfer-requests/{requestId}/approve` | Admin, StudentAffairs | Phê duyệt bàn giao Chủ nhiệm | `{"reviewNote": ""}` |
| `POST` | `/api/clubs/transfer-requests/{requestId}/reject` | Admin, StudentAffairs | Từ chối bàn giao | `{"reviewNote": ""}` |

---

### 3. HOẠT ĐỘNG & ĐIỂM DANH (ACTIVITIES & ATTENDANCE)
* **Base Path:** `{{baseUrl}}/api/activities` và `{{baseUrl}}/api/clubs/{clubId}/activities`

| Method | Endpoint | Quyền hạn | Mô tả | Request Body / Query Params |
| :--- | :--- | :--- | :--- | :--- |
| `GET` | `/api/activities` | Authenticated | Danh sách hoạt động sinh hoạt CLB | `?clubId=&status=&fromUtc=&toUtc=&includeStats=true` |
| `GET` | `/api/activities/{id}` | Authenticated | Chi tiết 1 hoạt động & thống kê tham gia | *(Param `id`)* |
| `GET` | `/api/clubs/{clubId}/activities/{activityId}/attendance` | ClubManager, Admin | Bảng danh sách điểm danh của một buổi sinh hoạt | `?search=&page=1&pageSize=50` |
| `PUT` | `/api/clubs/{clubId}/activities/{activityId}/attendance/{userId}` | ClubManager | Điểm danh cho từng thành viên lẻ | `{"status": "Present", "note": ""}` *(Present, Absent, Excused)* |
| `PUT` | `/api/clubs/{clubId}/activities/{activityId}/attendance/bulk` | ClubManager | Điểm danh hàng loạt cả danh sách thành viên | `{"items": [{"userId": 1, "status": "Present"}, {"userId": 2, "status": "Absent"}]}` |
| `POST` | `/api/activities/clubs/{clubId}/member-statistics` | ClubManager, Admin | Thống kê số buổi tham gia của danh sách thành viên | `{"members": [{"userId": 1}, {"userId": 2}]}` |
| `POST` | `/api/activities/clubs/{clubId}/member-statistics/detail`| ClubManager, Admin | Thống kê chi tiết chuyên cần & tỷ lệ tham gia | `{"members": [{"userId": 1}]}` |

---

### 4. BÁO CÁO & PHÊ DUYỆT (REPORTS & WORKFLOW)
* **Base Path:** `{{baseUrl}}/api/reports`

| Method | Endpoint | Quyền hạn | Mô tả | Request Body / Query Params |
| :--- | :--- | :--- | :--- | :--- |
| `GET` | `/api/reports` | BusinessAccess | Danh sách báo cáo (phân trang, lọc theo kỳ/CLB) | `?clubId=&status=&period=&tag=&page=1&pageSize=20` |
| `GET` | `/api/reports/summary` | BusinessAccess | Tổng hợp số lượng báo cáo (Draft, Submitted, Approved,...) | *(Tính toán tối ưu trực tiếp từ DB)* |
| `GET` | `/api/reports/aggregate` | StudentAffairs, Admin | Thống kê tổng hợp số hoạt động & người tham gia theo kỳ | `?period=FALL2026` |
| `GET` | `/api/reports/{id}` | BusinessAccess | Xem chi tiết nội dung 1 báo cáo (chi tiết, file, audit log) | *(Param `id`)* |
| `POST` | `/api/reports` | ClubManager, Treasurer | Tạo bản nháp báo cáo mới (Report Form) | `{"clubId": 1, "period": "FALL2026", "reportType": "MONTHLY", "tag": "MONTHLY", "details": [...]}` |
| `PUT` | `/api/reports/{id}` | ClubManager, Treasurer | Cập nhật nội dung bản nháp báo cáo | `{"period": "FALL2026", "reportType": "", "details": [...]}` |
| `DELETE` | `/api/reports/{id}` | ClubManager, Admin | Xóa bản nháp báo cáo | *(Param `id`)* |

#### Quy trình Nộp & Phê duyệt Báo cáo (Workflow State Machine):
| Method | Endpoint | Quyền hạn | Mô tả | Request Body |
| :--- | :--- | :--- | :--- | :--- |
| `POST` | `/api/reports/{id}/submit` | Author/Manager | Nộp báo cáo định kỳ lên Nhà trường $\to$ Kích hoạt Outbox Event | *(Không có body)* |
| `POST` | `/api/reports/{id}/submit-future-event`| ClubManager | Nộp kế hoạch sự kiện tương lai (xin ngân sách) | `{"budgetRequestedAmount": 5000000, "budgetDescription": ""}` |
| `POST` | `/api/reports/{id}/finance-review` | Treasurer | Thủ quỹ CLB rà soát trước khi gửi trường duyệt | `{"decision": "Approve", "note": "Ngân sách hợp lý"}` |
| `POST` | `/api/reports/{id}/review` | StudentAffairs, Admin | Cán bộ Phòng CTSV tiếp nhận thẩm định báo cáo | `{"reviewerName": "", "reviewNote": "Đang xem xét"}` |
| `POST` | `/api/reports/{id}/approve` | StudentAffairs, Admin | **Phê duyệt Báo cáo chính thức** (Tự động sinh Activity & Budget) | `{"feedbackMessage": "Đạt chuẩn"}` |
| `POST` | `/api/reports/{id}/reject` | Reviewer / Manager | Từ chối báo cáo / Yêu cầu sửa đổi | `{"feedbackMessage": "Cần bổ sung hóa đơn minh chứng"}` |

#### Quản lý File Báo cáo & File đính kèm:
| Method | Endpoint | Quyền hạn | Mô tả | Request Body (FormData) |
| :--- | :--- | :--- | :--- | :--- |
| `POST` | `/api/reports/upload` | ClubManager | Upload tệp báo cáo hoàn chỉnh (PDF, DOCX) tạo nháp | `FormData: file, clubId, period, reportType` |
| `GET` | `/api/reports/{id}/file` | BusinessAccess | Lấy thông tin metadata của file báo cáo chính | *(Param `id`)* |
| `GET` | `/api/reports/{id}/file/download` | BusinessAccess | Tải file báo cáo chính gốc về máy | *(Param `id`)* |
| `GET` | `/api/reports/{id}/file/preview` | BusinessAccess | Xem trực tiếp PDF/ảnh trên trình duyệt | *(Param `id`)* |
| `PUT` | `/api/reports/{id}/file` | ClubManager | Thay thế file báo cáo cũ bằng file mới | `FormData: file` |
| `DELETE` | `/api/reports/{id}/file` | ClubManager | Xóa file báo cáo chính | *(Param `id`)* |
| `POST` | `/api/reports/{id}/attachments/upload`| ClubManager | Tải lên tệp minh chứng đính kèm (hóa đơn, ảnh) | `FormData: file, reportDetailId` |
| `GET` | `/api/reports/{id}/attachments/{attachmentId}/download`| BusinessAccess | Tải file đính kèm | *(Param `id`, `attachmentId`)* |
| `DELETE` | `/api/reports/{id}/attachments/{attachmentId}`| ClubManager | Xóa file minh chứng đính kèm | *(Param `id`, `attachmentId`)* |

---

### 5. BẢNG XẾP HẠNG KPI & HẠN NỘP (KPIS & DEADLINES)
* **Base Path:** `{{baseUrl}}/api/kpis` và `{{baseUrl}}/api/deadlines`

| Method | Endpoint | Quyền hạn | Mô tả | Request Body / Query Params |
| :--- | :--- | :--- | :--- | :--- |
| `GET` | `/api/kpis/leaderboard` | BusinessAccess | **Bảng xếp hạng KPI toàn bộ CLB** (Gọi gRPC song song đa luồng) | `?period=FALL2026` |
| `GET` | `/api/kpis/rules` | BusinessAccess | Xem thể lệ & công thức tính điểm KPI của trường | *(Không có body)* |
| `GET` | `/api/deadlines` | BusinessAccess | Danh sách các mốc hạn nộp báo cáo các kỳ | *(Không có body)* |
| `GET` | `/api/deadlines/{period}` | BusinessAccess | Hạn nộp báo cáo của một kỳ cụ thể | *(Param `period`)* |
| `POST` | `/api/deadlines` | StudentAffairs, Admin | Thiết lập hạn chót nộp báo cáo kỳ mới | `{"period": "FALL2026", "dueDate": "2026-10-31"}` |
| `PUT` | `/api/deadlines/{period}` | StudentAffairs, Admin | Gia hạn / Cập nhật hạn chót nộp | `{"dueDate": "2026-11-05"}` |

---

### 6. QUẢN LÝ TÀI CHÍNH & QUYẾT TOÁN (FINANCE & PROPOSALS)
* **Base Path:** `{{baseUrl}}/api/finance`

| Method | Endpoint | Quyền hạn | Mô tả | Request Body / Query Params |
| :--- | :--- | :--- | :--- | :--- |
| `GET` | `/api/finance/proposals` | BusinessAccess | Danh sách đề xuất xin cấp kinh phí hoạt động | `?clubId=&status=` |
| `GET` | `/api/finance/proposals/{id}` | BusinessAccess | Chi tiết 1 đề xuất kinh phí | *(Param `id`)* |
| `POST` | `/api/finance/proposals` | ClubManager, Treasurer | Lập dự trù kinh phí mới | `{"clubId": 1, "title": "", "requestedAmount": 5000000, "description": ""}` |
| `POST` | `/api/finance/proposals/{id}/submit` | Treasurer | Trình đề xuất lên Nhà trường | *(Param `id`)* |
| `POST` | `/api/finance/proposals/{id}/manager-review` | ClubManager | Chủ nhiệm duyệt trước khi gửi trường | `{"decision": "Approve", "note": ""}` |
| `POST` | `/api/finance/proposals/{id}/review` | StudentAffairs, Admin | Nhà trường phê duyệt hạn mức kinh phí cấp | `{"decision": "Approve", "approvedAmount": 4500000, "reviewNote": ""}` |
| `GET` | `/api/finance/settlements` | BusinessAccess | Danh sách hồ sơ quyết toán chi tiêu thực tế | `?proposalId=&status=` |
| `GET` | `/api/finance/settlements/{id}` | BusinessAccess | Chi tiết hồ sơ quyết toán và hóa đơn | *(Param `id`)* |
| `POST` | `/api/finance/settlements` | Treasurer | Tạo hồ sơ hoàn ứng / quyết toán kinh phí | `{"budgetProposalId": 1, "totalSpent": 4200000, "receiptUrl": ""}` |
| `POST` | `/api/finance/settlements/{id}/submit`| Treasurer | Nộp chứng từ quyết toán | *(Param `id`)* |
| `POST` | `/api/finance/settlements/{id}/review`| StudentAffairs, Admin | Kế toán nhà trường phê duyệt giải ngân | `{"decision": "Approve", "reviewNote": "Chứng từ hợp lệ"}` |
| `GET` | `/api/finance/transactions` | BusinessAccess | Sổ cái nhật ký thu chi của CLB | `?clubId=&fromUtc=&toUtc=` |
| `POST` | `/api/finance/transactions` | Treasurer | Ghi nhận một giao dịch phát sinh vào quỹ CLB | `{"clubId": 1, "amount": 500000, "type": "Income", "description": "Tài trợ"}` |

---

### 7. THÔNG BÁO THỜI GIAN THỰC (NOTIFICATIONS)
* **Base Path:** `{{baseUrl}}/api/notifications`

| Method | Endpoint | Quyền hạn | Mô tả | Request Body / Query Params |
| :--- | :--- | :--- | :--- | :--- |
| `GET` | `/api/notifications` | AllActors | Lấy danh sách thông báo gửi đến người dùng hiện tại | `?unreadOnly=true` |
| `PUT` | `/api/notifications/{id}/read` | AllActors | Đánh dấu 1 thông báo đã đọc | *(Param `id`)* |
| `PUT` | `/api/notifications/read-all` | AllActors | Đánh dấu tất cả thông báo là đã đọc | *(Không có body)* |

---

### 8. XUẤT BÁO CÁO & FILE (EXPORTS PDF / EXCEL)
* **Base Path:** `{{baseUrl}}/api/exports`

| Method | Endpoint | Quyền hạn | Mô tả | Request Body / Query Params |
| :--- | :--- | :--- | :--- | :--- |
| `GET` | `/api/exports` | Authenticated | Danh sách lịch sử các file đã xuất | `?status=&page=1&pageSize=20` |
| `GET` | `/api/exports/{id}` | Authenticated | Kiểm tra tiến độ xử lý xuất file (Pending/Completed/Failed) | *(Param `id`)* |
| `POST` | `/api/exports` | Authenticated | Yêu cầu xuất báo cáo ra file PDF hoặc Excel | `{"format": "Pdf", "reportId": 1}` *(Format: Pdf / Excel)* |
| `GET` | `/api/exports/{id}/download` | Authenticated | Tải file PDF/Excel hoàn chỉnh về máy | *(Param `id`)* |

---

### 9. KIỂM TRA SỨC KHỎE HỆ THỐNG (HEALTH & SYSTEM)
* **Base Path:** `{{baseUrl}}`

| Method | Endpoint | Quyền hạn | Mô tả |
| :--- | :--- | :--- | :--- |
| `GET` | `/` | Public | Thông tin cổng API Gateway: `{"service": "YARP API Gateway", "status": "running"}` |
| `GET` | `/health` | Public | Healthcheck tình trạng kết nối mạng của Gateway |

---

# 🚀 MẸO DÀNH CHO BẠN BÈ LÀM FRONTEND
1. Khi gọi các API nộp file đính kèm (`/api/reports/upload`, `/api/reports/{id}/attachments/upload`), nhớ dùng `FormData` và không set cứng header `Content-Type: application/json` để trình duyệt tự động điền `multipart/form-data; boundary=...`.
2. Khi gọi các API lấy file xem trước hoặc tải về (`/api/reports/{id}/file/preview`, `/api/exports/{id}/download`), cấu hình `responseType: 'blob'` trong Axios.
3. Khi deploy sang máy khác, **chỉ cần đổi duy nhất 1 dòng `VITE_API_BASE_URL` trong file `.env` của Frontend** là xong, toàn bộ 65+ API trên sẽ tự động hoạt động bình thường!
