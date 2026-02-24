# Security Policy

**Version:** 1.0  
**Last Updated:** February 24, 2026  
**Status:** ACTIVE

---

## 1. Reporting Security Vulnerabilities

If you discover a security vulnerability, **please do NOT open a public GitHub issue**. Instead:

### Responsible Disclosure

1. **Email:** security@company.com
2. **Include:**
   - Description of the vulnerability
   - Steps to reproduce
   - Potential impact
   - Your contact information
3. **Do NOT:** Include working exploits in initial report
4. **Timeline:** We will acknowledge within 24 hours and provide estimated fix date

### What Happens Next

- **Days 1-3:** Initial triage and acknowledgment
- **Days 3-7:** Fix development (if applicable)
- **Days 7-14:** Security patch release
- **Day 14+:** Public disclosure (coordinated)

---

## 2. Supported Versions

### Current Support Matrix

| Version | Release Date | End of Support | Security Updates |
|---------|-------------|-----------------|------------------|
| 2.0.x   | 2026-02-24  | 2027-02-24      | ✅ Yes          |
| 1.9.x   | 2025-12-01  | 2026-02-24      | ⚠️ Limited       |
| 1.8.x   | 2025-09-15  | 2025-12-01      | ❌ No            |

**Recommendation:** Always use the latest version for security patches.

---

## 3. Security Best Practices

### For Developers

✅ **DO:**
- Use pinned base image versions
- Run containers as non-root users
- Scan images for vulnerabilities regularly
- Rotate credentials quarterly
- Keep dependencies updated
- Use secrets management (KeyVault, Vault, etc.)
- Enable CSRF protection on APIs
- Validate all inputs
- Log security events
- Review code before merging

❌ **DON'T:**
- Hardcode credentials or secrets
- Use floating base image tags
- Run containers as root
- Log sensitive data
- Commit `.env` files
- Use default passwords
- Disable HTTPS in production
- Expose database ports publicly
- Trust user input without validation
- Share credentials via email/chat

### For Operations

✅ **DO:**
- Implement network segmentation
- Use load balancers for SSL/TLS termination
- Enable container image scanning in registry
- Monitor for security vulnerabilities
- Keep container runtime updated
- Implement resource limits
- Enable audit logging
- Use firewall rules
- Implement network policies
- Monitor container logs

❌ **DON'T:**
- Expose internal services to the internet
- Use weak database passwords
- Run multiple customers' data in same container
- Disable security scanning
- Mount Docker socket in containers
- Allow privilege escalation
- Skip container base image updates
- Expose secrets in logs
- Use same credentials across environments
- Disable authentication

---

## 4. Dependency Security

### Vulnerability Scanning

All dependencies are scanned with:
- **Trivy:** For Docker image vulnerabilities
- **Dependabot:** For code dependency updates
- **npm audit:** For JavaScript dependencies
- **pip audit:** For Python dependencies

### Update Policy

| Severity | Timeline | Action |
|----------|----------|--------|
| CRITICAL | Immediate | Emergency patch release |
| HIGH | 24-48 hours | Next patch release |
| MEDIUM | 1-2 weeks | Quarterly release |
| LOW | Next release | Plan for next release |

### Reporting Dependency Issues

```bash
# Report vulnerability
npm audit --production
pip audit
dotnet list package --vulnerable
```

---

## 5. Container Security

### Base Image Selection

- **node:22.13.0-alpine:** Official Node image, regularly patched
- **python:3.12.1-slim:** Official Python image, lightweight
- **nginx:1.27.0-alpine:** Official Nginx, security updates
- **mcr.microsoft.com/dotnet/aspnet:9.0.0:** Microsoft official runtime

### Image Scanning Strategy

1. **Pre-Push:** `trivy image ./src/service/Dockerfile`
2. **Registry:** Automated scanning on push
3. **Kubernetes:** Automated image verification policies

---

## 6. Production Requirements

### Before Production Deployment

- [ ] All secrets rotated
- [ ] HTTPS/TLS configured
- [ ] Firewalls configured
- [ ] Network policies applied
- [ ] Monitoring enabled
- [ ] Backup configured
- [ ] Disaster recovery plan created
- [ ] Security audit completed
- [ ] Penetration testing passed
- [ ] Compliance requirements met

### Runtime Security

```bash
# Kubernetes security policy
apiVersion: policy/v1beta1
kind: PodSecurityPolicy
metadata:
  name: restricted
spec:
  privileged: false
  allowPrivilegeEscalation: false
  requiredDropCapabilities:
    - ALL
  volumes:
    - 'configMap'
    - 'emptyDir'
    - 'projected'
    - 'secret'
    - 'downwardAPI'
    - 'persistentVolumeClaim'
  hostNetwork: false
  hostIPC: false
  hostPID: false
  runAsUser:
    rule: 'MustRunAsNonRoot'
  seLinux:
    rule: 'MustRunAs'
```

---

## 7. Network Security

### Exposed Services

| Service | Port | Access | Protocol |
|---------|------|--------|----------|
| .NET API | 5000 | Load Balancer | HTTP/HTTPS |
| Flask API | 5001 | Load Balancer | HTTP/HTTPS |
| Angular UI | 80 | Public | HTTP/HTTPS |
| SQL Server | 1433 | Internal Only | TDS |

### Network Policies (Kubernetes)

```yaml
# Only allow app-to-db traffic
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: database-access
spec:
  podSelector:
    matchLabels:
      tier: database
  policyTypes:
  - Ingress
  ingress:
  - from:
    - podSelector:
        matchLabels:
          tier: backend
    ports:
    - protocol: TCP
      port: 1433
```

---

## 8. Data Protection

### Encryption

- **In Transit:** TLS 1.3+ required
- **At Rest:** Database encryption enabled
- **Backup:** Encrypted with AES-256

### PII/Sensitive Data Handling

- ✅ Hash passwords with bcrypt/PBKDF2
- ✅ Encrypt sensitive fields in database
- ✅ Use column-level encryption for PII
- ✅ Implement data retention policies
- ✅ GDPR compliance for EU users
- ✅ Regular security audits

---

## 9. Compliance

### Standards Compliance

- **OWASP Top 10:** Regular review and fixes
- **CWE/SANS:** Monitor for new vulnerabilities
- **GDPR:** Privacy by design
- **SOC 2:** Audit trail maintained
- **PCI DSS:** If processing payments

### Audit Trail

All sensitive operations logged:
```
- User authentication
- Credential access
- Configuration changes
- API calls to external services
- Database access
- Security events
```

---

## 10. Incident Response

### Incident Classification

1. **Critical:** Service down, data breach, active attack
2. **High:** Degraded performance, vulnerability discovered
3. **Medium:** Minor issue, no immediate impact
4. **Low:** Informational, future improvement

### Response Procedure

1. Detect → Alert on-call team
2. Assess → Determine impact and scope
3. Contain → Prevent further damage
4. Eradicate → Remove root cause
5. Recover → Restore service
6. Post-Mortem → Document and improve

### Incident Contact

- **On-Call:** See PagerDuty schedule
- **Email:** incidents@company.com
- **Slack:** #security-incidents

---

## 11. Third-Party Dependencies

### API Keys Management

| Service | Key | Rotation | Owner |
|---------|-----|----------|-------|
| OpenFigi | API Key | Quarterly | Finance Team |
| Encryption | Keys | Quarterly | DevOps |

All keys stored in Azure KeyVault with audit logging.

---

## 12. Credential Rotation Schedule

```
January:  SQL Server passwords
April:    JWT secrets, API keys
July:     Encryption keys
October:  All other secrets
```

---

## 13. Security Training

All team members required:
- [ ] OWASP secure coding (annually)
- [ ] Container security (annually)
- [ ] Incident response procedures (annually)
- [ ] Phishing awareness (quarterly)

---

## 14. Acknowledgments

We thank the following for responsible disclosures:
- [List of acknowledged security researchers]

---

## 15. Contact

- **Security Questions:** security@company.com
- **Report Vulnerability:** [security report form]
- **General Support:** support@company.com

---

**Last Reviewed:** February 24, 2026  
**Next Review:** August 24, 2026
