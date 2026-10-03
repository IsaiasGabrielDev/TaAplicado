// The Perfil questionnaire: same questions for every user; answers live in agent/profile.json.
// renderProfile() turns the answers into the "## Candidate profile" markdown Claude reads.
// Loaded by the page and by Node (to seed CLAUDE.md), hence the module guard at the bottom.
'use strict';

const PROFILE_TEMPLATE = [
  { title: 'Identificação', fields: [
    { id: 'nome', label: 'Nome completo', type: 'text' },
    { id: 'localizacao', label: 'Cidade, estado e país', type: 'text' },
    { id: 'email', label: 'E-mail para candidaturas', type: 'text' },
    { id: 'telefone', label: 'Telefone', type: 'text' },
    { id: 'links', label: 'LinkedIn, portfólio ou site', type: 'text' },
  ] },
  { title: 'Carreira', fields: [
    { id: 'cargoAtual', label: 'Cargo atual e empresa', type: 'text' },
    { id: 'cargosAnteriores', list: true, label: 'Cargos anteriores (cargo, empresa, período)', type: 'textarea' },
    { id: 'cargoDesejado', label: 'Que vaga você procura?', type: 'text' },
    { id: 'foco', label: 'Foco da vaga', type: 'select', pref: true, options: ['A mesma função, em qualquer setor', 'Só o setor em que já trabalho'], hint: 'Ex.: recepcionista de clínica veterinária também recebe vagas de recepção em escritório, clínica médica ou advocacia.' },
    { id: 'anosDev', label: 'Anos de experiência na função que você procura', type: 'number' },
    { id: 'anosTi', label: 'Anos de experiência na área, no total', type: 'number', hint: 'Inclui funções parecidas ou de apoio. Ex.: 3 como analista financeiro, 6 em finanças.' },
    { id: 'lideranca', label: 'Experiência com liderança de pessoas', type: 'textarea', hint: 'Em branco se não tiver.' },
  ] },
  { title: 'Competências e ferramentas', fields: [
    { id: 'anosTech', label: 'Anos de experiência por competência ou ferramenta', type: 'table', hint: 'O que as vagas da sua área perguntam: Excel, SAP, AutoCAD, vendas B2B, atendimento, C#… O que ficar fora desta lista vira aviso.' },
    { id: 'techUsadas', list: true, label: 'Outras competências e ferramentas que já usei (sem tempo definido)', type: 'textarea' },
    { id: 'techNao', label: 'Competências e ferramentas que não domino', type: 'text', hint: 'Para estas o Claude responde “não” sem perguntar.' },
    { id: 'negocio', list: true, label: 'Setores e mercados em que já trabalhei', type: 'textarea' },
  ] },
  { title: 'Idiomas e formação', fields: [
    { id: 'ingles', label: 'Nível de inglês', type: 'select', options: ['Básico (A1–A2)', 'Intermediário (B1)', 'Intermediário avançado (B2)', 'Avançado (C1)', 'Fluente (C2)'] },
    { id: 'outrosIdiomas', label: 'Outros idiomas', type: 'text' },
    { id: 'formacao', list: true, label: 'Formação (curso, instituição, conclusão)', type: 'textarea' },
    { id: 'certificacoes', list: true, label: 'Certificações', type: 'textarea', hint: 'Em branco: o Claude nunca cita certificação.' },
    { id: 'registro', label: 'Registro profissional (CRM, OAB, CREA, CRC, COREN…)', type: 'text', hint: 'Número e estado. Escreva “não se aplica” se a sua profissão não exige.' },
  ] },
  { title: 'Contrato e logística', fields: [
    { id: 'contrato', label: 'Tipo de contrato aceito', type: 'select', options: ['CLT', 'PJ', 'CLT ou PJ'] },
    { id: 'pretensaoClt', label: 'Pretensão salarial mensal CLT (R$ bruto)', type: 'number' },
    { id: 'pretensaoPj', label: 'Pretensão PJ', type: 'text', hint: 'Em branco: cada pergunta de PJ vira aviso para você decidir.' },
    { id: 'remuneracaoObs', label: 'Observações sobre remuneração', type: 'textarea' },
    { id: 'inicio', label: 'Disponibilidade para início / aviso prévio', type: 'text' },
    { id: 'modelo', label: 'Modelo de trabalho', type: 'select', options: ['Remoto', 'Híbrido', 'Presencial', 'Qualquer um'] },
    { id: 'mudanca', label: 'Aceita mudar de cidade?', type: 'select', options: ['Sim', 'Não', 'Depende da vaga'] },
    { id: 'cidadesPresencial', label: 'Cidades onde aceita trabalhar presencial ou híbrido', type: 'text', hint: 'Até onde dá para ir todo dia. Ex.: Mauá, Santo André, São Paulo. Vaga presencial em outra cidade é pulada.' },
    { id: 'viagens', label: 'Aceita viajar a trabalho?', type: 'select', options: ['Sim', 'Não', 'Eventualmente'] },
    { id: 'autorizadoBr', label: 'Autorizado a trabalhar no Brasil?', type: 'select', options: ['Sim', 'Não'] },
    { id: 'visto', label: 'Autorização em outros países / precisa de patrocínio de visto?', type: 'text' },
    { id: 'cnh', label: 'Possui CNH?', type: 'select', options: ['Sim', 'Não'] },
  ] },
  { title: 'Apresentação', fields: [
    { id: 'resumo', label: 'Resumo profissional (“fale sobre você”)', type: 'textarea' },
    { id: 'respostasProntas', list: true, label: 'Respostas prontas para perguntas frequentes', type: 'textarea' },
    { id: 'nuncaDizer', list: true, label: 'O que nunca deve ser dito', type: 'textarea' },
  ] },
];

const NOT_GIVEN = '(não informado)';

/** Table answers are stored as "Tecnologia = anos" lines. */
function tableRows(value) {
  return String(value ?? '').split('\n').map(l => l.trim()).filter(Boolean).map(l => {
    const i = l.lastIndexOf('=');
    return i < 0 ? [l, ''] : [l.slice(0, i).trim(), l.slice(i + 1).trim()];
  });
}

// pref: a search preference, not a form answer, so leaving it blank never turns into an aviso.
function profileGaps(values) {
  return PROFILE_TEMPLATE.flatMap(s => s.fields).filter(f => !f.pref && !String(values?.[f.id] ?? '').trim());
}

function renderProfile(values) {
  const out = ['<!-- Gerado pelo app a partir de agent/profile.json. Edite pela tela Perfil, não aqui. -->'];
  for (const s of PROFILE_TEMPLATE) {
    out.push('', `### ${s.title}`);
    for (const f of s.fields) {
      const v = String(values?.[f.id] ?? '').trim();
      if (!v) { out.push(`- **${f.label}:** ${NOT_GIVEN}`); continue; }
      if (f.type === 'table') {
        out.push(`- **${f.label}:**`, '  | Competência | Anos |', '  |---|---|',
          ...tableRows(v).map(([t, y]) => `  | ${t} | ${y || NOT_GIVEN} |`));
      } else if (v.includes('\n')) {
        out.push(`- **${f.label}:**`, ...v.split('\n').map(l => `  ${l}`));
      } else {
        out.push(`- **${f.label}:** ${v}`);
      }
    }
  }
  return out.join('\n');
}

if (typeof module !== 'undefined') module.exports = { PROFILE_TEMPLATE, renderProfile, profileGaps, tableRows };
